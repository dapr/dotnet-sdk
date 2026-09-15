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

// Regression coverage for https://github.com/dapr/dotnet-sdk/issues/1898
//
// An abstract *closed* (non-generic) workflow isolates the IsAbstract filter independent of
// the open-generic filter. The source generator must skip it, so neither its type name nor
// its declared canonical name may appear in the generated versioning registry.
//
// Gated to C# 16 / .NET 14 (which ships with the Dapr SDK 1.19 release) — this scenario is
// not exercised on prior target frameworks. The Dapr SDK 1.19 release will add .NET 14
// support; until that merge, this test is dormant (the type and test are compiled out).

#if NET14_0_OR_GREATER
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning;

internal static class AbstractClosedWorkflowConstants
{
    public const string CanonicalName = "AbstractClosedWorkflow";
}

[WorkflowVersion(CanonicalName = AbstractClosedWorkflowConstants.CanonicalName, Version = "1")]
internal abstract class AbstractClosedWorkflow : Workflow<string, string>
{
}

public sealed class AbstractClosedWorkflowRegressionTests
{
    /// <summary>
    /// The abstract closed (non-generic) workflow must be excluded from the generated
    /// versioning registry: its declared canonical name must not be a registry key, and its
    /// type name must not appear in any registered workflow list.
    /// </summary>
    [MinimumDaprRuntimeFact("1.19")]
    public void AbstractClosedWorkflow_IsExcludedFromVersioningRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprWorkflowVersioning();
        services.AddDaprWorkflowBuilder(configureRuntime: _ => { });

        using var provider = services.BuildServiceProvider();
        var registry = GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(provider);

        Assert.False(registry.ContainsKey(AbstractClosedWorkflowConstants.CanonicalName),
            "The abstract closed workflow's canonical name must not appear as a registry key.");
        Assert.DoesNotContain(
            registry.SelectMany(kv => kv.Value),
            v => v.Contains(nameof(AbstractClosedWorkflow), StringComparison.Ordinal));
    }
}
#endif
