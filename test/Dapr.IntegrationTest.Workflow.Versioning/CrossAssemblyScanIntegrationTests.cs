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

using Dapr.IntegrationTest.Workflow.Versioning.ReferenceWorkflows;
using Dapr.Workflow.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning;

public sealed class CrossAssemblyScanIntegrationTests
{
    [Fact]
    public void ShouldDiscoverReferencedWorkflowsWhenEnabled()
    {
        var services = new ServiceCollection();
        services.AddDaprWorkflowVersioning();

        using var provider = services.BuildServiceProvider();
        var registry = GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(provider);

        Assert.True(registry.TryGetValue(CrossAssemblyWorkflowConstants.CanonicalName, out var versions));
        Assert.NotNull(versions);
        Assert.Contains(versions, v => v.EndsWith("CrossAppWorkflowV1", StringComparison.Ordinal));
        Assert.Contains(versions, v => v.EndsWith("CrossAppWorkflowV2", StringComparison.Ordinal));

        var latest = NormalizeWorkflowTypeName(versions![0]);
        Assert.Equal("CrossAppWorkflowV2", latest);
    }

    private static string NormalizeWorkflowTypeName(string typeName)
    {
        var trimmed = typeName;
        if (trimmed.StartsWith("global::", StringComparison.Ordinal))
        {
            trimmed = trimmed["global::".Length..];
        }

        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 ? trimmed[(lastDot + 1)..] : trimmed;
    }
}
