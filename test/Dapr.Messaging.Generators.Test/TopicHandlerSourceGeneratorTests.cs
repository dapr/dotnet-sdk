// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Messaging.Generators.Test;

/// <summary>
/// Verifies that <see cref="TopicHandlerSourceGenerator"/> emits the expected dispatchers, registry,
/// manifest, JSON context, and DI extension for a variety of [DaprTopic] usages.
/// </summary>
public class TopicHandlerSourceGeneratorTests
{
    private static MetadataReference AbstractionsReference()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Dapr.Messaging.Abstractions.dll");
        return MetadataReference.CreateFromFile(path);
    }

    private static Task<(string GeneratedSource, Diagnostic[] Diagnostics)> RunAsync(string userSource)
    {
        // Build a complete reference set from the assemblies already loaded into the test host
        // (System.Runtime, System.Text.Json, Microsoft.Extensions.*, etc.) plus the abstractions
        // assembly under test. This avoids the fragility of hand-picking individual framework refs.
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(MetadataReference? (a) => MetadataReference.CreateFromFile(a.Location))
            .Where(r => r is not null)
            .Cast<MetadataReference>()
            .ToList();
        references.Add(AbstractionsReference());

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(userSource) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new TopicHandlerSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var generated = string.Empty;
        foreach (var tree in runResult.GeneratedTrees.Where(tree => tree.FilePath.EndsWith("DaprMessagingTopicHandlers.g.cs")))
        {
            generated = tree.ToString();
        }

        return Task.FromResult((generated, runResult.Diagnostics.ToArray()));
    }

    [Fact]
    public async Task SingleHandler_SingleTopic_EmitsDispatcherAndRegistry()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders")]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.True(generated.Length > 0, $"Generated source was empty. Diagnostics: {string.Join("; ", diagnostics.Select(d => d.ToString()))}");
        Assert.Contains("OrderHandler_pubsub_orders_Dispatcher", generated);
        Assert.Contains("DaprMessagingSubscriberRegistry", generated);
        Assert.Contains("AddGeneratedSubscribers", generated);
        Assert.Contains("typeof(global::MyApp.Order)", generated);
        Assert.Contains("DaprMessagingJsonContext", generated);
    }

    [Fact]
    public async Task SingleHandler_MultipleTopics_EmitsMultipleDispatchers()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Event { public string Type { get; set; } = string.Empty; }

[DaprTopic("pubsub", "created")]
[DaprTopic("pubsub", "updated")]
public class MultiHandler : ITopicHandler<Event>
{
    public Task<TopicResponseAction> HandleAsync(Event message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("MultiHandler_pubsub_created_Dispatcher", generated);
        Assert.Contains("MultiHandler_pubsub_updated_Dispatcher", generated);
    }

    [Fact]
    public async Task ProgrammaticDelivery_EmitsDescriptorWithDeliveryMode()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Customer { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "customers", Delivery = DeliveryMode.Programmatic)]
public class CustomerHandler : ITopicHandler<Customer>
{
    public Task<TopicResponseAction> HandleAsync(Customer message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Delivery = global::Dapr.Messaging.DeliveryMode.Programmatic", generated);
    }

    [Fact]
    public async Task HttpDelivery_EmitsRouteAndDeliveryMode()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Customer { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "customers", Delivery = DeliveryMode.Http, Route = "events/customers")]
public class CustomerHandler : ITopicHandler<Customer>
{
    public Task<TopicResponseAction> HandleAsync(Customer message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Delivery = global::Dapr.Messaging.DeliveryMode.Http", generated);
        Assert.Contains("Route = \"events/customers\"", generated);
    }

    [Fact]
    public async Task MatchWithoutPriority_ReportsDapr1605()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Foo { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "foos", Match = "event.type == \"v2\"")]
public class FooHandler : ITopicHandler<Foo>
{
    public Task<TopicResponseAction> HandleAsync(Foo message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (_, diagnostics) = await RunAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "DAPR1605");
    }

    // -----------------------------------------------------------------------
    //  Additional generator tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyCompilation_EmitsSkeletonWithoutDispatchers()
    {
        const string source = """
namespace MyApp;
public class NoHandler { }
""";

        var (generated, _) = await RunAsync(source);

        Assert.Contains("DaprMessagingSubscriberRegistry", generated);
        Assert.Contains("Array.Empty<ITopicDispatcher>()", generated);
        Assert.Contains("AddGeneratedSubscribers", generated);
    }

    [Fact]
    public async Task DeadLetterTopic_EmitsDescriptorWithDeadLetter()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", DeadLetterTopic = "orders-dlq")]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("DeadLetterTopic = \"orders-dlq\"", generated);
    }

    [Fact]
    public async Task BulkSubscribe_EmitsBulkSubscribeOptions()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", BulkSubscribe = true, MaxMessagesCount = 50, MaxAwaitDurationMs = 200)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("BulkSubscribe = new BulkSubscribeOptions", generated);
        Assert.Contains("MaxMessagesCount = 50", generated);
        Assert.Contains("MaxAwaitDurationMs = 200", generated);
    }

    [Fact]
    public async Task MatchWithPriority_NoDiagnostic()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Match = "event.type == \"v2\"", Priority = 1)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (_, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1605");
    }

    [Fact]
    public async Task HandlerNotImplementingITopicHandler_ReportsDapr1601()
    {
        const string source = """
using Dapr.Messaging;

namespace MyApp;

[DaprTopic("pubsub", "orders")]
public class NotAHandler { }
""";

        var (_, diagnostics) = await RunAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "DAPR1601");
    }

    [Fact]
    public async Task DuplicateTopicAndDelivery_ReportsDapr1603()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders")]
public class HandlerA : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}

[DaprTopic("pubsub", "orders")]
public class HandlerB : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (_, diagnostics) = await RunAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "DAPR1603");
    }

    [Fact]
    public async Task TwoArgHandlerVariant_EmitsDispatcher()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }
public class OrderResult { public bool Success { get; set; } }

[DaprTopic("pubsub", "orders")]
public class OrderHandler : ITopicHandler<Order, OrderResult>
{
    public Task<OrderResult> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(new OrderResult { Success = true });
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("OrderHandler_pubsub_orders_Dispatcher", generated);
    }

    [Fact]
    public async Task MultipleHandlers_EmitsAllDispatchers()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }
public class Customer { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders")]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}

[DaprTopic("pubsub", "customers")]
public class CustomerHandler : ITopicHandler<Customer>
{
    public Task<TopicResponseAction> HandleAsync(Customer message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("OrderHandler_pubsub_orders_Dispatcher", generated);
        Assert.Contains("CustomerHandler_pubsub_customers_Dispatcher", generated);
    }

    [Fact]
    public async Task MetadataAttribute_CorrelatedByMetadataKeys()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

[assembly: DaprTopicMetadata("region", "us-east")]

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", MetadataKeys = new[] { "region" })]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        // The metadata should appear in the subscription manifest JSON.
        Assert.Contains("region", generated);
    }

    [Fact]
    public async Task EnableRawPayload_EmitsDescriptorWithRawPayload()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", EnableRawPayload = true)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("EnableRawPayload = true", generated);
    }

    [Fact]
    public async Task HttpDelivery_DefaultsRouteToTopicNameWhenOmitted()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Http)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Delivery = global::Dapr.Messaging.DeliveryMode.Http", generated);
        Assert.Contains("Route = \"orders\"", generated);
    }

    [Fact]
    public async Task HttpDelivery_WithCustomRoute_EmitsCustomRoute()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Http, Route = "/api/v2/orders")]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var (generated, diagnostics) = await RunAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Delivery = global::Dapr.Messaging.DeliveryMode.Http", generated);
        Assert.Contains("Route = \"/api/v2/orders\"", generated);
    }
}
