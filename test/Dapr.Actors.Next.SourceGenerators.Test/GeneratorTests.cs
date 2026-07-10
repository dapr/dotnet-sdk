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
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.SourceGenerators.Sample;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.SourceGenerators.Test;

public sealed class GeneratorTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void Generated_output_contains_proxy_dispatcher_registry_and_generic_serializer_calls()
    {
        var source = """
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.State;
            using Dapr.Actors.Next.Core.Activation;
            using System.Threading;
            using System.Threading.Tasks;

            namespace SnapshotSample;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task AddAsync(CartItem item, CancellationToken cancellationToken = default);
                Task<CartSummary> GetAsync(CancellationToken cancellationToken = default);
                Task<int> CountAsync(int left, int right, CancellationToken cancellationToken = default);
            }

            public sealed record CartItem(string Sku, int Quantity);
            public sealed record CartSummary(int Count);

            [DaprActor("Cart", ContractVersion = 3)]
            public sealed class CartActor(ActorActivationContext context) : Actor, ICartActor
            {
                protected override ActorId Id => context.ActorId;
                protected override IActorStateAccessor State => context.State;
                public Task AddAsync(CartItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
                public Task<CartSummary> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CartSummary(1));
                public Task<int> CountAsync(int left, int right, CancellationToken cancellationToken = default) => Task.FromResult(left + right);
            }
            """;

        var generated = RunGenerator(CreateCompilation("SnapshotSample", source), scanReferences: false);

        Assert.Contains("GeneratedCartActorProxy", generated);
        Assert.Contains("CartActorDispatcher", generated);
        Assert.Contains("?? @\"Cart\"", generated);
        Assert.Contains("ActorTypeDescriptor(actorType, 3", generated);
        Assert.Contains("ActorHeaders.Empty", generated);
        Assert.Contains("CompleteResultAsync<global::SnapshotSample.CartSummary>", generated);
        Assert.Contains("SerializeToBytes<TResult>(result)", generated);
        Assert.DoesNotContain("public async global::System.Threading.Tasks.ValueTask<global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse> DispatchAsync", generated);
        Assert.Contains("DeserializeFromBytes<global::SnapshotSample.CartItem>(request.Payload)", generated);
        Assert.Contains("SerializeToBytes<global::SnapshotSample.CartItem>(item)", generated);
        Assert.Contains("JsonSerializable(typeof(global::SnapshotSample.CartItem))", generated);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Cross_assembly_discovery_is_gated_by_scan_property()
    {
        var library = CreateCompilation("ExternalActors", """
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.State;
            using Dapr.Actors.Next.Core.Activation;
            using System.Threading;
            using System.Threading.Tasks;

            namespace ExternalActors;

            [GenerateActorClient]
            public interface IExternalActor : IActor
            {
                Task<int> PingAsync(int value, CancellationToken cancellationToken = default);
            }

            [DaprActor("External")]
            public sealed class ExternalActor(ActorActivationContext context) : Actor, IExternalActor
            {
                protected override ActorId Id => context.ActorId;
                protected override IActorStateAccessor State => context.State;
                public Task<int> PingAsync(int value, CancellationToken cancellationToken = default) => Task.FromResult(value);
            }
            """);
        var image = new MemoryStream();
        var emit = library.Emit(image);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        image.Position = 0;

        var app = CreateCompilation("ExternalApp", "namespace ExternalApp; public sealed class Marker {}", MetadataReference.CreateFromImage(image.ToArray()));

        var off = RunGenerator(app, scanReferences: false);
        var on = RunGenerator(app, scanReferences: true);

        Assert.DoesNotContain("ExternalActorDispatcher", off);
        Assert.Contains("ExternalActorDispatcher", on);
        Assert.Contains("?? @\"External\"", on);
        Assert.Contains("ActorTypeDescriptor(actorType", on);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Generator_handles_edge_shapes_without_emitting_invalid_code()
    {
        var source = """
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.State;
            using System.Threading;
            using System.Threading.Tasks;

            namespace EdgeSample;

            [GenerateActorClient]
            public interface WorkerActor : IActor
            {
                ValueTask ResetAsync();
                ValueTask<int> ReadAsync();
                Task OptionalAsync(int value = 1);
                void Ignored();
            }

            [GenerateActorClient]
            public interface INoCtorActor : IActor
            {
                Task PingAsync();
            }

            [GenerateActorClient]
            public interface IGenericActor<T> : IActor
            {
                Task PingAsync();
            }

            [DaprActor]
            public sealed class WorkerActorImpl(ActorId id) : Actor, WorkerActor
            {
                protected override ActorId Id => id;
                protected override IActorStateAccessor State => throw new System.NotSupportedException();
                public ValueTask ResetAsync() => ValueTask.CompletedTask;
                public ValueTask<int> ReadAsync() => ValueTask.FromResult(7);
                public Task OptionalAsync(int value = 1) => Task.CompletedTask;
                public void Ignored() { }
            }

            [DaprActor]
            public sealed class NoCtorActor : Actor, INoCtorActor
            {
                protected override ActorId Id => ActorId.Create("no-ctor");
                protected override IActorStateAccessor State => throw new System.NotSupportedException();
                public Task PingAsync() => Task.CompletedTask;
            }

            [DaprActor]
            public sealed class NotRegisteredActor : Actor
            {
                protected override ActorId Id => ActorId.Create("none");
                protected override IActorStateAccessor State => throw new System.NotSupportedException();
            }
            """;

        var generated = RunGenerator(CreateCompilation("EdgeSample", source), scanReferences: null);

        Assert.Contains("GeneratedWorkerActorProxy", generated);
        Assert.Contains("WorkerActorImplDispatcher", generated);
        Assert.Contains("NoCtorActorDispatcher", generated);
        Assert.Contains("?? @\"WorkerActorImpl\"", generated);
        Assert.Contains("ActorTypeDescriptor(actorType, 1", generated);
        Assert.Contains("(sp, actorId) => new global::EdgeSample.WorkerActorImpl(actorId)", generated);
        Assert.Contains("(sp, actorId) => new global::EdgeSample.NoCtorActor()", generated);
        Assert.DoesNotContain("GenericActor", generated);
        Assert.DoesNotContain("Ignored()", generated);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Generator_emits_distinct_actor_type_registrations_for_shared_interfaces()
    {
        var source = """
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.State;
            using System.Threading;
            using System.Threading.Tasks;

            namespace SharedInterfaceSample;

            [GenerateActorClient]
            public interface ISharedActor : IActor
            {
                Task PingAsync(CancellationToken cancellationToken = default);
            }

            [DaprActor("SharedAlpha", ContractVersion = 2)]
            public sealed class SharedAlphaActor : Actor, ISharedActor
            {
                protected override ActorId Id => ActorId.Create("alpha");
                protected override IActorStateAccessor State => throw new System.NotSupportedException();
                public Task PingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

            [DaprActor("SharedBeta", ContractVersion = 3)]
            public sealed class SharedBetaActor : Actor, ISharedActor
            {
                protected override ActorId Id => ActorId.Create("beta");
                protected override IActorStateAccessor State => throw new System.NotSupportedException();
                public Task PingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

            public sealed class SharedStateV1
            {
                public int Value { get; set; }
            }

            public sealed class SharedStateV2
            {
                public int Value { get; set; }
            }

            public sealed class SharedStateUpcaster : IActorStateUpcaster<SharedStateV1, SharedStateV2>
            {
                public ValueTask<SharedStateV2> UpcastAsync(SharedStateV1 state, CancellationToken cancellationToken = default) =>
                    ValueTask.FromResult(new SharedStateV2 { Value = state.Value });
            }
            """;

        var generated = RunGenerator(CreateCompilation("SharedInterfaceSample", source), scanReferences: false);

        Assert.Equal(2, CountOccurrences(generated, "builder.Add(actorType, typeof(global::SharedInterfaceSample.ISharedActor)"));
        Assert.Equal(2, CountOccurrences(generated, "ActorTypeDescriptor(actorType,"));
        Assert.Contains("SharedAlphaActorDispatcherExplicitRegistration?.ActorTypeName ?? @\"SharedAlpha\"", generated);
        Assert.Contains("SharedBetaActorDispatcherExplicitRegistration?.ActorTypeName ?? @\"SharedBeta\"", generated);
        Assert.Contains("ActorTypeDescriptor(actorType, 2", generated);
        Assert.Contains("ActorTypeDescriptor(actorType, 3", generated);
        Assert.Contains("if (options.EnableAutoStateMigrationRegistration)", generated);
        Assert.Contains("IActorStateUpcaster<global::SharedInterfaceSample.SharedStateV1, global::SharedInterfaceSample.SharedStateV2>", generated);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Generator_supports_actor_implementing_multiple_generate_client_interfaces()
    {
        var source = """
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.State;
            using System.Threading;
            using System.Threading.Tasks;

            namespace MultiInterfaceSample;

            [GenerateActorClient]
            public interface IDocumentReader : IActor
            {
                Task<DocumentSummary> ReadAsync(CancellationToken cancellationToken = default);
            }

            [GenerateActorClient]
            public interface IDocumentWriter : IActor
            {
                Task WriteAsync(DocumentPatch patch, CancellationToken cancellationToken = default);
            }

            public sealed record DocumentSummary(int Version);
            public sealed record DocumentPatch(string Field, string Value);

            [DaprActor("Document")]
            public sealed class DocumentActor : Actor, IDocumentReader, IDocumentWriter
            {
                protected override ActorId Id => ActorId.Create("doc");
                protected override IActorStateAccessor State => throw new System.NotSupportedException();
                public Task<DocumentSummary> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new DocumentSummary(1));
                public Task WriteAsync(DocumentPatch patch, CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(CreateCompilation("MultiInterfaceSample", source), scanReferences: false, assertGeneratedCompiles: true);

        // One proxy is emitted per interface, each keyed on its own interface type in the factory.
        Assert.Contains("GeneratedDocumentReaderProxy", generated);
        Assert.Contains("GeneratedDocumentWriterProxy", generated);
        Assert.Contains("typeof(TActor) == typeof(global::MultiInterfaceSample.IDocumentReader)", generated);
        Assert.Contains("typeof(TActor) == typeof(global::MultiInterfaceSample.IDocumentWriter)", generated);

        // A single dispatcher covers the union of both interfaces' methods.
        Assert.Equal(1, CountOccurrences(generated, "class DocumentActorDispatcher"));
        Assert.Contains("case @\"ReadAsync\"", generated);
        Assert.Contains("case @\"WriteAsync\"", generated);
        Assert.Contains("CompleteResultAsync<global::MultiInterfaceSample.DocumentSummary>", generated);
        Assert.Contains("DeserializeFromBytes<global::MultiInterfaceSample.DocumentPatch>(request.Payload)", generated);

        // The actor type is registered exactly once (a duplicate would throw at runtime), against the
        // representative (alphabetically first) interface.
        Assert.Equal(1, CountOccurrences(generated, "builder.Add(actorType, typeof(global::MultiInterfaceSample.IDocumentReader)"));
        Assert.DoesNotContain("builder.Add(actorType, typeof(global::MultiInterfaceSample.IDocumentWriter)", generated);
        Assert.Equal(1, CountOccurrences(generated, "ActorTypeDescriptor(actorType,"));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Generator_emits_additive_state_migration_family_metadata_and_aot_delegates()
    {
        var source = """
            namespace MigrationSample;

            public sealed class MyState
            {
                public string Name { get; set; } = "";
            }

            public sealed class MyStateV2
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
            }

            public sealed class MyStateV3
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
                public bool Active { get; set; }
            }
            """;

        var generated = RunGenerator(CreateCompilation("MigrationSample", source), scanReferences: false, assertGeneratedCompiles: true);

        Assert.Contains("ActorStateMigrationFamily(@\"MigrationSample.MyState\"", generated);
        Assert.Contains("ActorStateMigrationNode(0, typeof(global::MigrationSample.MyState)", generated);
        Assert.Contains("ActorStateMigrationNode(1, typeof(global::MigrationSample.MyStateV2)", generated);
        Assert.Contains("ActorStateMigrationNode(2, typeof(global::MigrationSample.MyStateV3)", generated);
        Assert.Contains("ActorStateMigrationEdge(0, 1, null)", generated);
        Assert.Contains("ActorStateMigrationEdge(1, 2, null)", generated);
        Assert.Contains("TryAddSingleton<global::Dapr.Actors.Next.Abstractions.State.Versioning.IActorStateMigrator>", generated);
        Assert.DoesNotContain("if (!options.DisableStateMigration)", generated);
        Assert.Contains("UpcastStateGenerated_MigrationSample_MyState_0_1", generated);
        Assert.Contains("new global::MigrationSample.MyStateV2", generated);
        Assert.Contains("Name = source.Name", generated);
        Assert.Contains("Age = source.Age", generated);
        Assert.Contains("DeserializeFromBytes<global::Dapr.Actors.Next.Abstractions.State.ActorStateEnvelope<global::MigrationSample.MyState>>(payload)", generated);
        Assert.Contains("DeserializeFromBytes<global::MigrationSample.MyState>(payload)", generated);
        Assert.Contains("JsonSerializable(typeof(global::Dapr.Actors.Next.Abstractions.State.ActorStateEnvelope<global::MigrationSample.MyState>))", generated);
        Assert.Contains("JsonSerializable(typeof(global::Dapr.Actors.Next.Abstractions.State.ActorStatePlainEnvelope<global::MigrationSample.MyStateV3>))", generated);
        Assert.Contains("JsonSerializable(typeof(global::MigrationSample.MyStateV2))", generated);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Sample_generated_registration_dispatcher_factory_and_proxy_run_end_to_end()
    {
        _ = typeof(CalculatorActor);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CalculatorDependency>();
        // This test exercises the generated proxy end-to-end against only the library (no sidecar), so route
        // proxy invocations through the in-process runtime rather than the (unreachable) gRPC sidecar client.
        services.AddDaprActors(options => options.EnableSidecarTransport = false);
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var registry = provider.GetRequiredService<IActorRegistry>();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var proxyFactory = provider.GetRequiredService<IActorProxyFactory>();
        ActorProxy.Configure(proxyFactory);

        Assert.True(registry.TryGet("Calculator", out var descriptor));
        Assert.Equal(2, descriptor.ContractVersion);
        Assert.Contains(descriptor.Methods, method => method.Name == "SumAsync" && method.Parameters.Count == 3);
        Assert.NotNull(provider.GetRequiredService<IActorStateUpcaster<CalculatorStateV1, CalculatorStateV2>>());
        Assert.NotNull(provider.GetRequiredService<IActorStateMigrator>());

        var proxy = ActorProxy.Create<ICalculatorActor>(ActorId.Create("calc-1"), "Calculator");
        Assert.Equal(4, await proxy.SumAsync(1, 2));
        await proxy.AddAsync(new CalculationInput(5));
        var result = await proxy.GetAsync();

        Assert.Equal(6, result.Value);
        Assert.Equal(["generated", "calc-1"], result.Tags);

        var weakResult = await runtime.InvokeAsync("Calculator", "calc-2", "SumAsync", System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { left = 2, right = 3 }), new Dictionary<string, string>());
        Assert.NotNull(weakResult);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Explicit_actor_name_overrides_auto_registered_name_once()
    {
        _ = typeof(CalculatorActor);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CalculatorDependency>();
        services.AddDaprActors(options => options.Actors.RegisterActor<CalculatorActor>("RenamedCalculator"));
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var registry = provider.GetRequiredService<IActorRegistry>();
        var runtime = provider.GetRequiredService<IActorRuntime>();

        Assert.False(registry.TryGet("Calculator", out _));
        Assert.True(registry.TryGet("RenamedCalculator", out var descriptor));
        Assert.Equal(typeof(CalculatorActor), descriptor.ImplementationType);
        Assert.Single(registry.Actors);

        var weakResult = await runtime.InvokeAsync("RenamedCalculator", "calc-2", "SumAsync", System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { left = 2, right = 3 }), new Dictionary<string, string>());
        Assert.NotNull(weakResult);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Auto_registration_flags_gate_hosted_actors_and_upcasters_independently()
    {
        _ = typeof(CalculatorActor);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CalculatorDependency>();
        services.AddDaprActors(options =>
        {
            options.EnableAutoActorRegistration = false;
            options.EnableAutoStateMigrationRegistration = false;
            options.Actors.RegisterActor<CalculatorActor>();
        });
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var registry = provider.GetRequiredService<IActorRegistry>();
        var runtime = provider.GetRequiredService<IActorRuntime>();

        Assert.True(registry.TryGet("Calculator", out _));
        Assert.Empty(provider.GetServices<IActorStateUpcaster<CalculatorStateV1, CalculatorStateV2>>());

        var weakResult = await runtime.InvokeAsync("Calculator", "calc-3", "SumAsync", System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { left = 4, right = 5 }), new Dictionary<string, string>());
        Assert.NotNull(weakResult);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Disable_state_migration_keeps_generated_migrator_available_for_tolerant_reads()
    {
        _ = typeof(CalculatorActor);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CalculatorDependency>();
        services.AddDaprActors(options =>
        {
            options.EnableSidecarTransport = false;
            options.DisableStateMigration = true;
        });
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IActorStateMigrator>());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Auto_actor_registration_off_still_installs_runtime_services()
    {
        _ = typeof(CalculatorActor);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CalculatorDependency>();
        services.AddDaprActors(options => options.EnableAutoActorRegistration = false);
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var registry = provider.GetRequiredService<IActorRegistry>();

        Assert.NotNull(provider.GetRequiredService<IActorRuntime>());
        Assert.Empty(registry.Actors);
    }

    private static string RunGenerator(CSharpCompilation compilation, bool? scanReferences, bool assertGeneratedCompiles = false)
    {
        var generator = new ActorsNextSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None),
            optionsProvider: new TestAnalyzerConfigOptionsProvider(scanReferences));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        if (assertGeneratedCompiles)
        {
            Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        var result = driver.GetRunResult().Results.Single();
        return string.Join(Environment.NewLine, result.GeneratedSources.Select(source => source.SourceText.ToString()));
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, string source, params MetadataReference[] additionalReferences)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(additionalReferences);

        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class TestAnalyzerConfigOptionsProvider(bool? scanReferences) : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions globalOptions = new(scanReferences);

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyAnalyzerConfigOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyAnalyzerConfigOptions.Instance;
    }

    private sealed class TestAnalyzerConfigOptions(bool? scanReferences) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (key == "build_property.DaprActorsScanReferences" && scanReferences.HasValue)
            {
                value = scanReferences.Value ? "true" : "false";
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private sealed class EmptyAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public static readonly EmptyAnalyzerConfigOptions Instance = new();

        public override bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return false;
        }
    }
}
