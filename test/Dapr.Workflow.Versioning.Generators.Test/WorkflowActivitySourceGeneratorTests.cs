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
/// Activity discovery tests for <see cref="WorkflowSourceGenerator"/>.
/// </summary>
/// <remarks>
/// The activity discovery pipeline mirrors the workflow pipeline and already filtered
/// abstract/open-generic types at discovery (the pattern the workflow fix followed). These
/// tests guard that behavior against regressions, covering abstract, open-generic, and
/// concrete activities, plus an activities-only project (no workflows) to verify the
/// registry is still emitted and well-formed when the workflow side is empty.
/// </remarks>
public sealed class WorkflowActivitySourceGeneratorTests
{
    /// <summary>
    /// An abstract activity must not be registered — it cannot be instantiated.
    /// </summary>
    [Fact]
    public async Task AbstractActivity_IsExcluded()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;

namespace MyApp;

public abstract class AbstractActivity : WorkflowActivity<string, string> { }
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.True(string.IsNullOrEmpty(generated),
            $"Expected no generated registry source for an abstract-only activity, but got:{Environment.NewLine}{generated}");
    }

    /// <summary>
    /// An open-generic activity must not be registered — its unbound type parameters cannot
    /// be referenced in the generated registration call.
    /// </summary>
    [Fact]
    public async Task OpenGenericActivity_IsExcluded()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;

namespace MyApp;

public sealed class GenericActivity<T> : WorkflowActivity<T, string>
{
    public override Task<string> RunAsync(WorkflowActivityContext context, T input)
        => Task.FromResult(string.Empty);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.True(string.IsNullOrEmpty(generated),
            $"Expected no generated registry source for an open-generic activity, but got:{Environment.NewLine}{generated}");
    }

    /// <summary>
    /// A concrete activity must be registered by the generated registry.
    /// </summary>
    [Fact]
    public async Task ConcreteActivity_IsRegistered()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;

namespace MyApp;

public sealed class PlainActivity : WorkflowActivity<string, string>
{
    public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        => Task.FromResult(input);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.False(string.IsNullOrEmpty(generated), "Expected generated registry source for the concrete activity.");
        Assert.Contains("PlainActivity", generated, StringComparison.Ordinal);
        GeneratorTestHarness.AssertNoSyntaxErrors(generated);
    }

    /// <summary>
    /// An activities-only project (no workflows) must still emit a well-formed registry
    /// containing the activity registration. This guards the <c>workflows.Count == 0</c>
    /// early-return and the empty-<c>concreteWorkflows</c> path in <c>RegisterAlias</c>
    /// (no orphan <c>else</c> branch) when the workflow side is empty.
    /// </summary>
    [Fact]
    public async Task ActivitiesOnly_NoWorkflows_EmitsActivityRegistry()
    {
        const string source = """
using System.Threading.Tasks;
using Dapr.Workflow;

namespace MyApp;

public sealed class FirstActivity : WorkflowActivity<string, string>
{
    public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        => Task.FromResult(input);
}

public sealed class SecondActivity : WorkflowActivity<int, bool>
{
    public override Task<bool> RunAsync(WorkflowActivityContext context, int input)
        => Task.FromResult(input > 0);
}
""";

        var (generated, diagnostics) = await GeneratorTestHarness.RunAsync(source);

        GeneratorTestHarness.AssertNoErrorDiagnostics(diagnostics);
        Assert.False(string.IsNullOrEmpty(generated), "Expected generated registry source for the activities-only project.");
        Assert.Contains("FirstActivity", generated, StringComparison.Ordinal);
        Assert.Contains("SecondActivity", generated, StringComparison.Ordinal);
        // No workflows are present, so no workflow registration call should be emitted.
        Assert.DoesNotContain("RegisterWorkflow", generated, StringComparison.Ordinal);
        Assert.Contains("RegisterActivity", generated, StringComparison.Ordinal);
        GeneratorTestHarness.AssertNoSyntaxErrors(generated);
    }
}
