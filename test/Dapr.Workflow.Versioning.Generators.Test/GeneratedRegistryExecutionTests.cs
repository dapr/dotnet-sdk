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

using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Workflow.Versioning.Generators.Test;

public sealed class GeneratedRegistryExecutionTests
{
    internal static string WorkflowSource(string ns, string type, string family, string version, string extraAttribute = "") => $$"""
        namespace {{ns}}
        {
            [global::Dapr.Workflow.Versioning.WorkflowVersion(CanonicalName = "{{family}}", Version = "{{version}}"{{extraAttribute}})]
            public sealed class {{type}} : global::Dapr.Workflow.Workflow<string, string>
            {
                public override global::System.Threading.Tasks.Task<string> RunAsync(global::Dapr.Workflow.WorkflowContext context, string input)
                    => global::System.Threading.Tasks.Task.FromResult("{{ns}}:" + input);
            }
        }
        """;

    private static ServiceProvider Services(Action<WorkflowVersioningOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprWorkflow();
        services.AddDaprWorkflowVersioning(configure);
        return services.BuildServiceProvider();
    }

    private static void Register(string source, IServiceProvider services)
    {
        var assembly = GeneratorTestHarness.Load(GeneratorTestHarness.Generate(source).Compilation);
        GeneratorTestHarness.Register(assembly, services.GetRequiredService<WorkflowRuntimeOptions>(), services);
    }

    private static IWorkflow Create(IServiceProvider services, string name)
    {
        Assert.True(services.GetRequiredService<IWorkflowRouterRegistry>().Contains(name), name);
        Assert.True(services.GetRequiredService<IWorkflowsFactory>()
            .TryCreateWorkflow(new TaskIdentifier(name), services, out var workflow, out var error), error?.ToString());
        return workflow!;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CrossAssemblySelectionPreservesQualifiedAndSimpleReplayNames(bool reverse, bool selectMinimum)
    {
        using var services = Services(o =>
        {
            if (selectMinimum)
                o.DefaultSelector = _ => new MinimumSelector();
        });
        var sources = new[] { WorkflowSource("Old", "OrderV1", "Order", "1"), WorkflowSource("New", "OrderV2", "Order", "2") };
        foreach (var source in reverse ? sources.Reverse() : sources)
            Register(source, services);
        var selected = selectMinimum ? "Old.OrderV1" : "New.OrderV2";
        Assert.Equal(selected, Create(services, "Order").GetType().FullName);
        var router = services.GetRequiredService<IWorkflowRouterRegistry>();
        Assert.True(router.TryResolveLatest(out var latest));
        Assert.Equal("global::" + selected, latest);
        Assert.Equal(selected, Create(services, latest).GetType().FullName);
        Assert.Equal("Old:payload", await Create(services, "global::Old.OrderV1").RunAsync(null!, "payload"));
        Assert.Equal("New:payload", await Create(services, "OrderV2").RunAsync(null!, "payload"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameSimpleNamesNeverReplaceQualifiedLatest(bool splitAssemblies)
    {
        using var services = Services();
        var oldSource = WorkflowSource("A", "Process", "Order", "1");
        var newSource = WorkflowSource("B", "Process", "Order", "2");
        Register(splitAssemblies ? oldSource : oldSource + newSource, services);
        if (splitAssemblies)
            Register(newSource, services);
        var router = services.GetRequiredService<IWorkflowRouterRegistry>();
        Assert.True(router.TryResolveLatest(out var latest));
        Assert.Equal("global::B.Process", latest);
        Assert.Equal("B:data", await Create(services, latest).RunAsync(null!, "data"));
        Assert.Equal("A.Process", Create(services, "Process").GetType().FullName);
        Assert.Equal("A.Process", Create(services, "global::A.Process").GetType().FullName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FamiliesSurviveActivityOnlyAssembliesAndRegistrationOrder(bool reverse)
    {
        using var services = Services();
        var sources = new[]
        {
            WorkflowSource("A", "AlphaV1", "Alpha", "1"),
            WorkflowSource("B", "BetaV1", "Beta", "1"),
            """
            public sealed class OnlyActivity : Dapr.Workflow.WorkflowActivity<string, string>
            {
                public override System.Threading.Tasks.Task<string> RunAsync(Dapr.Workflow.WorkflowActivityContext context, string input)
                    => System.Threading.Tasks.Task.FromResult(input);
            }
            """
        };
        foreach (var source in reverse ? sources.Reverse() : sources)
            Register(source, services);
        Assert.Equal("A.AlphaV1", Create(services, "Alpha").GetType().FullName);
        Assert.Equal("B.BetaV1", Create(services, "Beta").GetType().FullName);
        Assert.False(services.GetRequiredService<IWorkflowRouterRegistry>().TryResolveLatest(out _));
        Assert.True(services.GetRequiredService<IWorkflowsFactory>()
            .TryCreateActivity(new TaskIdentifier("OnlyActivity"), services, out _, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitRegistrationsWinBeforeOrAfterGeneratedContributions(bool explicitFirst)
    {
        using var services = Services();
        var options = services.GetRequiredService<WorkflowRuntimeOptions>();
        void Explicit()
        {
            options.RegisterWorkflow<string, string>("Order", (_, input) => Task.FromResult("explicit:" + input));
            options.RegisterWorkflow<string, string>("CustomAlias", (_, input) => Task.FromResult("alias:" + input));
        }
        if (explicitFirst) Explicit();
        Register(WorkflowSource("A", "OrderV1", "Order", "1"), services);
        Register(WorkflowSource("B", "OrderV2", "Order", "2"), services);
        if (!explicitFirst) Explicit();
        // Factory initialization finalizes the catalog, including explicit aliases.
        services.GetRequiredService<IWorkflowsFactory>();
        Assert.Equal("explicit:x", await Create(services, "Order").RunAsync(null!, "x"));
        Assert.Equal("alias:x", await Create(services, "CustomAlias").RunAsync(null!, "x"));
        Assert.Equal("B.OrderV2", Create(services, "global::B.OrderV2").GetType().FullName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FamilyStrategyOverridesGlobalStrategyAcrossAssemblies(bool reverse)
    {
        using var services = Services(o => o.DefaultStrategy = _ => new ReverseNumericStrategy());
        var sources = new[]
        {
            WorkflowSource("A", "OrderV1", "Order", "1", ", StrategyType = typeof(global::Dapr.Workflow.Versioning.NumericVersionStrategy)"),
            WorkflowSource("B", "OrderV2", "Order", "2")
        };
        foreach (var source in reverse ? sources.Reverse() : sources)
            Register(source, services);
        Assert.Equal("B.OrderV2", Create(services, "Order").GetType().FullName);
    }

    [Fact]
    public void RepeatedContributionsAreIdempotentAndProvidersAreIsolated()
    {
        var assembly = GeneratorTestHarness.Load(GeneratorTestHarness.Generate(WorkflowSource("A", "OrderV1", "Order", "1")).Compilation);
        using var first = Services();
        using var second = Services();
        var options = first.GetRequiredService<WorkflowRuntimeOptions>();
        GeneratorTestHarness.Register(assembly, options, first);
        GeneratorTestHarness.Register(assembly, options, first);
        Register(WorkflowSource("B", "OtherV1", "Other", "1"), second);
        Assert.Equal("A.OrderV1", Create(first, "Order").GetType().FullName);
        Assert.False(second.GetRequiredService<IWorkflowRouterRegistry>().Contains("Order"));
        Assert.Equal("B.OtherV1", Create(second, "Other").GetType().FullName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupSelectsOnceFromAllContributionsAndCombinesLegacyRoutes(bool reverse)
    {
        var selector = new CompleteFamilySelector();
        using var services = Services(o => o.DefaultSelector = _ => selector);
        var options = services.GetRequiredService<WorkflowRuntimeOptions>();
        var assemblies = new[]
        {
            GeneratorTestHarness.Load(GeneratorTestHarness.Generate(WorkflowSource("A", "OrderV1", "Order", "1")).Compilation),
            GeneratorTestHarness.Load(GeneratorTestHarness.Generate(WorkflowSource("B", "OrderV2", "Order", "2")).Compilation)
        };
        foreach (var assembly in reverse ? assemblies.Reverse() : assemblies)
        {
            WorkflowVersioningRegistry.Register((runtime, provider) =>
            {
                if (ReferenceEquals(runtime, options))
                    GeneratorTestHarness.Register(assembly, runtime, provider);
            });
        }
        foreach (var family in new[] { "LegacyAlpha", "LegacyBeta" })
        {
            WorkflowVersioningRegistry.Register((runtime, provider) =>
            {
                if (!ReferenceEquals(runtime, options))
                    return;
                runtime.RegisterWorkflow<string, string>(family, (_, input) => Task.FromResult(input));
                provider.GetRequiredService<IWorkflowRouterRegistry>().UpdateRoutes(
                    new Dictionary<string, IReadOnlyList<string>> { [family] = new[] { family } });
            });
        }

        services.GetRequiredService<IWorkflowsFactory>();
        Assert.Equal(1, selector.Calls);
        Assert.Equal("B.OrderV2", Create(services, "Order").GetType().FullName);
        Create(services, "LegacyAlpha");
        Create(services, "LegacyBeta");
        Assert.False(services.GetRequiredService<IWorkflowRouterRegistry>().TryResolveLatest(out _));
    }

    [Fact]
    public void NamedFamilyStrategyOptionsApplyToAllAssemblyContributions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprWorkflow();
        services.AddDaprWorkflowVersioning();
        services.ConfigureStrategyOptions<DateVersionStrategyOptions>("dates", o => o.DateFormat = "ddMMyyyy");
        using var provider = services.BuildServiceProvider();
        Register(WorkflowSource("A", "Old", "Order", "31122025",
            ", StrategyType = typeof(global::Dapr.Workflow.Versioning.DateVersionStrategy), OptionsName = \"dates\""), provider);
        Register(WorkflowSource("B", "New", "Order", "01012026"), provider);
        Assert.Equal("B.New", Create(provider, "Order").GetType().FullName);
    }

    [Fact]
    public void ConflictingFamilyOptionsAcrossAssembliesFailInsteadOfDependingOnOrder()
    {
        using var services = Services();
        const string strategy = ", StrategyType = typeof(global::Dapr.Workflow.Versioning.NumericVersionStrategy), OptionsName = ";
        Register(WorkflowSource("A", "OrderV1", "Order", "1", strategy + "\"first\""), services);
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            Register(WorkflowSource("B", "OrderV2", "Order", "2", strategy + "\"second\""), services));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("Conflicting", error.InnerException.Message);
    }

    [Fact]
    public void RouterUpdateRoutesRetainsReplacementContractAndAcceptsCaseInsensitiveAliases()
    {
        var router = new WorkflowRouterRegistry();
        router.UpdateRoutes(new Dictionary<string, IReadOnlyList<string>> { ["Old"] = new[] { "global::Old.Version" } });
        Assert.True(router.Contains("GLOBAL::OLD.VERSION"));
        router.UpdateRoutes(new Dictionary<string, IReadOnlyList<string>> { ["New"] = new[] { "global::New.Version" } });
        Assert.False(router.Contains("Old"));
        Assert.True(router.TryResolveLatest(out var latest));
        Assert.Equal("global::New.Version", latest);
    }

    [Fact]
    public void StartupIncludesContributionsRegisteredWhileApplyingEarlierAssemblies()
    {
        using var services = Services();
        var options = services.GetRequiredService<WorkflowRuntimeOptions>();
        var first = GeneratorTestHarness.Load(GeneratorTestHarness.Generate(WorkflowSource("A", "OrderV1", "Order", "1")).Compilation);
        var second = GeneratorTestHarness.Load(GeneratorTestHarness.Generate(WorkflowSource("B", "OrderV2", "Order", "2")).Compilation);
        WorkflowVersioningRegistry.Register((runtime, provider) =>
        {
            if (!ReferenceEquals(runtime, options))
                return;
            GeneratorTestHarness.Register(first, runtime, provider);
            WorkflowVersioningRegistry.Register((laterOptions, laterProvider) =>
            {
                if (ReferenceEquals(laterOptions, options))
                    GeneratorTestHarness.Register(second, laterOptions, laterProvider);
            });
        });

        services.GetRequiredService<IWorkflowsFactory>();
        Assert.Equal("B.OrderV2", Create(services, "Order").GetType().FullName);
    }

    private sealed class CompleteFamilySelector : IWorkflowVersionSelector
    {
        public int Calls { get; private set; }
        public WorkflowVersionIdentity SelectLatest(string canonicalName,
            IReadOnlyCollection<WorkflowVersionIdentity> candidates, IWorkflowVersionStrategy strategy)
        {
            Calls++;
            Assert.Equal(2, candidates.Count);
            var selected = candidates.OrderByDescending(v => v.Version, strategy).First();
            // AssemblyName is optional in the existing public selector contract.
            return selected with { AssemblyName = null };
        }
    }

    private sealed class MinimumSelector : IWorkflowVersionSelector
    {
        public WorkflowVersionIdentity SelectLatest(string canonicalName,
            IReadOnlyCollection<WorkflowVersionIdentity> candidates, IWorkflowVersionStrategy strategy) =>
            candidates.OrderBy(v => v.Version, strategy).First();
    }

    private sealed class ReverseNumericStrategy : IWorkflowVersionStrategy
    {
        private readonly NumericVersionStrategy _inner = new();
        public bool TryParse(string typeName, out string canonicalName, out string version) =>
            _inner.TryParse(typeName, out canonicalName, out version);
        public int Compare(string? x, string? y) => _inner.Compare(y, x);
    }
}
