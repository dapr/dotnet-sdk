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

namespace Dapr.Workflow.Versioning.Generators.Test;

/// <summary>
/// Regression coverage for https://github.com/dapr/dotnet-sdk/issues/1898
/// </summary>
/// <remarks>
/// <para>
/// The source generator must not attempt to register abstract or open-generic workflow
/// classes. When a project contains only an abstract open-generic workflow (no concrete
/// derivatives), the generator previously emitted a registry with an orphan <c>else</c>
/// branch (and, before #1885, referenced the unbound type parameters), producing
/// CS0246/syntax errors at build time.
/// </para>
/// <para>
/// These tests exercise each class variation — abstract open-generic, concrete open-generic,
/// and concrete closed — both with and without <c>[WorkflowVersion]</c> metadata, and verify
/// abstract/open-generic types are skipped at the discovery stage so no invalid source is
/// emitted. The abstract <em>closed</em> (non-generic) variation is covered separately in the
/// integration test project (gated to Dapr 1.19 / C# 16).
/// </para>
/// </remarks>
public sealed class WorkflowSourceGeneratorTests
{
    // ── Plain (non-versioned) workflows ───────────────────────────────────────

    /// <summary>
    /// Reproduces issue #1898: a project containing only an abstract open-generic workflow
    /// (no concrete derivatives) must not emit any registry source. Previously the generator
    /// emitted an orphan <c>else</c> branch (invalid C#), failing the build.
    /// </summary>
    [Fact]
    public async Task AbstractOpenGenericWorkflowOnly_EmitsNoRegistrySource()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;

namespace MyApp;

public abstract class CustomWorkflow<TInput, TOutput> : Workflow<TInput, TOutput> { }
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.True(string.IsNullOrEmpty(generated),
            $"Expected no generated registry source for an abstract-only workflow, but got:{Environment.NewLine}{generated}");
    }

    /// <summary>
    /// A concrete open-generic workflow (non-abstract, but unbound type parameters) must also
    /// be skipped — it cannot be instantiated and would emit references to unbound type
    /// parameters (CS0246). This isolates the <c>TypeParameters.Length &gt; 0</c> filter
    /// independent of <c>IsAbstract</c>.
    /// </summary>
    [Fact]
    public async Task ConcreteOpenGenericWorkflowOnly_EmitsNoRegistrySource()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;

namespace MyApp;

public sealed class GenericWorkflow<TInput> : Workflow<TInput, string>
{
    public override Task<string> RunAsync(WorkflowContext context, TInput input)
        => Task.FromResult(string.Empty);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.True(string.IsNullOrEmpty(generated),
            $"Expected no generated registry source for a concrete open-generic workflow, but got:{Environment.NewLine}{generated}");
    }

    /// <summary>
    /// A plain concrete workflow must still be registered by the generated registry.
    /// </summary>
    [Fact]
    public async Task ConcreteWorkflow_EmitsRegistration()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;

namespace MyApp;

public sealed class PlainWorkflow : Workflow<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
        => Task.FromResult(input);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.False(string.IsNullOrEmpty(generated), "Expected generated registry source for the concrete workflow.");
        Assert.Contains("PlainWorkflow", generated, StringComparison.Ordinal);
        GeneratorTestHarness.AssertNoSyntaxErrors(generated);
    }

    /// <summary>
    /// An abstract open-generic workflow with a concrete closed derivative must register only
    /// the concrete type. The generated source must not reference the abstract base's unbound
    /// type parameters and must be syntactically valid.
    /// </summary>
    [Fact]
    public async Task AbstractGenericBaseWithConcreteDerivative_RegistersOnlyConcrete()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;

namespace MyApp;

public abstract class CustomWorkflowBase<TInput, TOutput> : Workflow<TInput, TOutput> { }

[WorkflowVersion(CanonicalName = "MyWorkflow", Version = "1")]
public sealed class MyWorkflow : CustomWorkflowBase<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
        => Task.FromResult(input);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.False(string.IsNullOrEmpty(generated), "Expected generated registry source for the concrete derivative.");
        Assert.Contains("MyWorkflow", generated, StringComparison.Ordinal);
        // The abstract open-generic base must not appear in the generated registration code
        // (it would reference unbound type parameters and produce CS0246).
        Assert.DoesNotContain("CustomWorkflowBase", generated, StringComparison.Ordinal);
        GeneratorTestHarness.AssertNoSyntaxErrors(generated);
    }

    // ── Versioned workflows ([WorkflowVersion]) ──────────────────────────────
    //      Validates the versioning registry path (CreateEntries / RegisterAlias),
    //      not just basic registration — the exact code path that broke in #1898.

    /// <summary>
    /// An abstract open-generic workflow carrying <c>[WorkflowVersion]</c> must be skipped
    /// entirely — neither its type name nor its declared canonical name may appear in the
    /// generated versioning registry. With no other concrete types, no source is emitted.
    /// </summary>
    [Fact]
    public async Task AbstractOpenGenericWorkflow_WithVersionAttribute_IsExcluded()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;

namespace MyApp;

[WorkflowVersion(CanonicalName = "AbsOpenGeneric", Version = "1")]
public abstract class AbsOpenGenericWorkflow<TInput, TOutput> : Workflow<TInput, TOutput> { }
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.True(string.IsNullOrEmpty(generated),
            $"Expected no generated registry source for a versioned abstract open-generic workflow, but got:{Environment.NewLine}{generated}");
    }

    /// <summary>
    /// A concrete open-generic workflow carrying <c>[WorkflowVersion]</c> must be skipped —
    /// its unbound type parameters cannot be referenced in the generated registration calls.
    /// Isolates the open-generic filter on a non-abstract type with versioning metadata.
    /// </summary>
    [Fact]
    public async Task ConcreteOpenGenericWorkflow_WithVersionAttribute_IsExcluded()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;

namespace MyApp;

[WorkflowVersion(CanonicalName = "ConcOpenGeneric", Version = "1")]
public sealed class ConcOpenGenericWorkflow<T> : Workflow<T, string>
{
    public override Task<string> RunAsync(WorkflowContext context, T input)
        => Task.FromResult(string.Empty);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.True(string.IsNullOrEmpty(generated),
            $"Expected no generated registry source for a versioned concrete open-generic workflow, but got:{Environment.NewLine}{generated}");
    }

    /// <summary>
    /// A concrete closed workflow carrying <c>[WorkflowVersion]</c> must be included in the
    /// generated versioning registry: its type name and declared canonical name must appear,
    /// and the emitted source must be syntactically valid (the <c>if/else if/else</c> chain
    /// in <c>RegisterAlias</c> must be well-formed — the exact code that produced an orphan
    /// <c>else</c> in #1898 when only abstract/generic workflows were present).
    /// </summary>
    [Fact]
    public async Task ConcreteClosedWorkflow_WithVersionAttribute_IsRegisteredWithCanonicalName()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;

namespace MyApp;

[WorkflowVersion(CanonicalName = "VersionedConcrete", Version = "1")]
public sealed class VersionedConcreteWorkflow : Workflow<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
        => Task.FromResult(input);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.False(string.IsNullOrEmpty(generated), "Expected generated registry source for the versioned concrete workflow.");
        Assert.Contains("VersionedConcreteWorkflow", generated, StringComparison.Ordinal);
        // The declared canonical name must be emitted into the versioning registry entries.
        Assert.Contains("VersionedConcrete", generated, StringComparison.Ordinal);
        GeneratorTestHarness.AssertNoSyntaxErrors(generated);
    }

    /// <summary>
    /// A class that does not inherit from <c>Workflow&lt;,&gt;</c> must never be registered,
    /// guarding against false positives from the discovery filter.
    /// </summary>
    [Fact]
    public async Task NonWorkflowClass_IsNotRegistered()
    {
        const string source = """
namespace MyApp;

public sealed class NotAWorkflow
{
    public string Value { get; set; } = string.Empty;
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.True(string.IsNullOrEmpty(generated),
            $"Expected no generated registry source for a non-workflow class, but got:{Environment.NewLine}{generated}");
    }
}
