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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Messaging.Analyzers.Test;

public class DaprMessagingAnalyzersTests
{
    private static MetadataReference AbstractionsReference()
        => MetadataReference.CreateFromFile(System.IO.Path.Combine(AppContext.BaseDirectory, "Dapr.Messaging.Abstractions.dll"));

    private static MetadataReference RuntimeReference()
        => MetadataReference.CreateFromFile(System.IO.Path.Combine(AppContext.BaseDirectory, "Dapr.Messaging.dll"));

    private static Task<Diagnostic[]> RunAsync(DiagnosticAnalyzer analyzer, string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference?)MetadataReference.CreateFromFile(a.Location))
            .Where(r => r is not null)
            .Cast<MetadataReference>()
            .ToList();
        references.Add(AbstractionsReference());
        references.Add(RuntimeReference());

        // DaprMessagingRegistration.Register takes an IServiceCollection, so binding that call
        // requires the DI abstractions assembly to be referenced.
        var diPath = typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location;
        if (!string.IsNullOrEmpty(diPath) && references.OfType<PortableExecutableReference>().All(r => r.FilePath != diPath))
        {
            references.Add(MetadataReference.CreateFromFile(diPath));
        }

        // Ensure System.Text.Json is referenced (it may not be loaded yet).
        var stjPath = typeof(System.Text.Json.JsonSerializer).Assembly.Location;
        if (!string.IsNullOrEmpty(stjPath) && references.OfType<PortableExecutableReference>().All(r => r.FilePath != stjPath))
        {
            references.Add(MetadataReference.CreateFromFile(stjPath));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzerDriver = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        return analyzerDriver.GetAnalyzerDiagnosticsAsync().ContinueWith(t => t.Result.ToArray());
    }

    [Fact]
    public async Task DAPR1610_FiresOnConflictingDeliveryModes()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Streaming)]
public class PullHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class PushHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new DuplicateTopicDeliveryAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1610");
    }

    [Fact]
    public async Task DAPR1610_DoesNotFireOnSameDeliveryMode()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Streaming)]
public class PullHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}

[DaprTopic("pubsub", "events", Delivery = DeliveryMode.Streaming)]
public class PullHandler2 : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new DuplicateTopicDeliveryAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1610");
    }

    [Fact]
    public async Task DAPR1611_FiresOnNonHandlerClass()
    {
        var source = """
using Dapr.Messaging;

namespace MyApp;

[DaprTopic("pubsub", "orders")]
public class NotAHandler { }
""";

        var diagnostics = await RunAsync(new HandlerNotImplementingITopicHandlerAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1611");
    }

    [Fact]
    public async Task DAPR1611_DoesNotFireOnValidHandler()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders")]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new HandlerNotImplementingITopicHandlerAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1611");
    }

    [Fact]
    public async Task DAPR1612_FiresOnUnregisteredMessageType()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders")]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new NonAotCompatibleMessageAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1612");
    }

    [Fact]
    public async Task DAPR1612_DoesNotFireOnPrimitiveMessage()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

[DaprTopic("pubsub", "orders")]
public class StringHandler : ITopicHandler<string>
{
    public Task<TopicResponseAction> HandleAsync(string message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new NonAotCompatibleMessageAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1612");
    }

    [Fact]
    public async Task DAPR1610_FiresOnConflictingModes_SameHandlerClass()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Streaming)]
[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class DualHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new DuplicateTopicDeliveryAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1610");
    }

    [Fact]
    public async Task DAPR1611_DoesNotFireOnTwoArgHandlerVariant()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }
public class Result { public bool Ok { get; set; } }

[DaprTopic("pubsub", "orders")]
public class OrderHandler : ITopicHandler<Order, Result>
{
    public Task<Result> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(new Result { Ok = true });
}
""";

        var diagnostics = await RunAsync(new HandlerNotImplementingITopicHandlerAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1611");
    }

    [Fact]
    public async Task DAPR1612_DoesNotFireWhenTypeRegisteredViaJsonSerializable()
    {
        var source = """
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;

public class Order { public string Id { get; set; } = string.Empty; }

[JsonSerializable(typeof(Order))]
internal sealed partial class MyJsonContext : JsonSerializerContext { }

[DaprTopic("pubsub", "orders")]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new NonAotCompatibleMessageAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1612");
    }

    [Fact]
    public async Task DAPR1611_FiresOnStruct()
    {
        var source = """
using Dapr.Messaging;

namespace MyApp;

[DaprTopic("pubsub", "orders")]
public struct NotAHandler { }
""";

        var diagnostics = await RunAsync(new HandlerNotImplementingITopicHandlerAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1611");
    }

    [Fact]
    public async Task DAPR1610_DoesNotFireOnDifferentTopics_SameDeliveryMode()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

namespace MyApp;
public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class HandlerA : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}

[DaprTopic("pubsub", "customers", Delivery = DeliveryMode.Programmatic)]
public class HandlerB : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct) => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new DuplicateTopicDeliveryAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1610");
    }

    // -----------------------------------------------------------------------
    //  DAPR1613: Missing MapDaprAppCallback Analyzer & CodeFix
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DAPR1613_FiresWhenProgrammaticHandlerMissingMapDaprAppCallback()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprMessaging();
var app = builder.Build();
app.Run();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new MissingMapDaprAppCallbackAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1613");
    }

    [Fact]
    public async Task DAPR1613_DoesNotFireWhenMapDaprAppCallbackIsPresent()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprMessaging();
var app = builder.Build();
app.MapDaprAppCallback();
app.Run();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new MissingMapDaprAppCallbackAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1613");
    }

    [Fact]
    public async Task DAPR1613_DoesNotFireWhenMapDaprMessagingIsPresent()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprMessaging();
var app = builder.Build();
app.MapDaprMessaging();
app.Run();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new MissingMapDaprAppCallbackAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1613");
    }

    [Fact]
    public async Task DAPR1613_DoesNotFireForStreamingOnly()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Streaming)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new MissingMapDaprAppCallbackAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1613");
    }

    [Fact]
    public async Task DAPR1613_CodeFix_InsertsMapDaprAppCallback()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Dapr.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprMessaging();
var app = builder.Build();
app.Run();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference?)MetadataReference.CreateFromFile(a.Location))
            .Where(r => r is not null)
            .Cast<MetadataReference>()
            .ToList();
        references.Add(AbstractionsReference());
        references.Add(RuntimeReference());

        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, references)
            .AddDocument(documentId, "Program.cs", source);

        var document = solution.GetDocument(documentId)!;
        var compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken);
        var analyzer = new MissingMapDaprAppCallbackAnalyzer();
        var driver = compilation!.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await driver.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "DAPR1613"));

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (a, _) => actions.Add(a),
            TestContext.Current.CancellationToken);

        var codeFixProvider = new MapDaprAppCallbackCodeFixProvider();
        await codeFixProvider.RegisterCodeFixesAsync(context);

        var action = Assert.Single(actions);
        Assert.Equal("Call app.MapDaprAppCallback()", action.Title);

        var operations = await action.GetOperationsAsync(TestContext.Current.CancellationToken);
        var applyOp = operations.OfType<ApplyChangesOperation>().Single();
        var newDoc = applyOp.ChangedSolution.GetDocument(documentId)!;
        var newSource = (await newDoc.GetTextAsync(TestContext.Current.CancellationToken)).ToString();

        Assert.Contains("app.MapDaprAppCallback();", newSource);
    }

    // -----------------------------------------------------------------------
    //  DAPR1614: Direct DaprMessagingRegistration Usage Analyzer
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DAPR1614_FiresWhenRegistrationCalledDirectly()
    {
        var source = """
using Dapr.Messaging;
using Microsoft.Extensions.DependencyInjection;

public static class Startup
{
    public static void Configure(IServiceCollection services)
    {
        DaprMessagingRegistration.Register(services, null, DaprMessagingFeatures.None);
    }
}
""";

        var diagnostics = await RunAsync(new DirectRegistrationUsageAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1614" && d.GetMessage().Contains("AddDaprMessaging()"));
    }

    [Fact]
    public async Task DAPR1614_DoesNotFireWhenRegistrationIsNotUsed()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapDaprAppCallback();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new DirectRegistrationUsageAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1614");
    }

    // -----------------------------------------------------------------------
    //  DAPR1615: Unused Subscriber Registration Analyzer
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DAPR1615_FiresForMapDaprAppCallbackWhenNoProgrammaticSubscribersExist()
    {
        var source = """
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapDaprAppCallback();
""";

        var diagnostics = await RunAsync(new UnusedDaprSubscriberRegistrationAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1615" && d.GetMessage().Contains("MapDaprAppCallback()"));
    }

    [Fact]
    public async Task DAPR1615_FiresForMapDaprHttpSubscriptionsWhenNoHttpSubscribersExist()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapDaprHttpSubscriptions();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new UnusedDaprSubscriberRegistrationAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1615" && d.GetMessage().Contains("MapDaprHttpSubscriptions()"));
    }

    [Fact]
    public async Task DAPR1615_DoesNotFireForMapDaprHttpSubscriptionsWhenHttpSubscriberExists()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapDaprHttpSubscriptions();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Http)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new UnusedDaprSubscriberRegistrationAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1615");
    }

    [Fact]
    public async Task DAPR1615_FiresForMapDaprMessagingWhenNoHttpOrProgrammaticSubscribersExist()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapDaprMessaging();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Streaming)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new UnusedDaprSubscriberRegistrationAnalyzer(), source);
        Assert.Contains(diagnostics, d => d.Id == "DAPR1615" && d.GetMessage().Contains("MapDaprMessaging()"));
    }

    [Fact]
    public async Task DAPR1615_DoesNotFireForMapDaprMessagingWhenHttpSubscriberExists()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapDaprMessaging();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Http)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new UnusedDaprSubscriberRegistrationAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1615");
    }

    [Fact]
    public async Task DAPR1615_DoesNotFireForMapDaprMessagingWhenProgrammaticSubscriberExists()
    {
        var source = """
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapDaprMessaging();

public class Order { public string Id { get; set; } = string.Empty; }

[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Programmatic)]
public class OrderHandler : ITopicHandler<Order>
{
    public Task<TopicResponseAction> HandleAsync(Order message, TopicContext context, CancellationToken ct)
        => Task.FromResult(TopicResponseAction.Success);
}
""";

        var diagnostics = await RunAsync(new UnusedDaprSubscriberRegistrationAnalyzer(), source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "DAPR1615");
    }
}
