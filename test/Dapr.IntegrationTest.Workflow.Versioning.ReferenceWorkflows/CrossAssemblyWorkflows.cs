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

using Dapr.Workflow;
using Dapr.Workflow.Versioning;

namespace Dapr.IntegrationTest.Workflow.Versioning.ReferenceWorkflows;

public static class CrossAssemblyWorkflowConstants
{
    public const string CanonicalName = "CrossAppWorkflow";
}

[WorkflowVersion(CanonicalName = CrossAssemblyWorkflowConstants.CanonicalName, Version = "1")]
public sealed class CrossAppWorkflowV1 : Workflow<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
    {
        return Task.FromResult($"v1:{input}");
    }
}

[WorkflowVersion(CanonicalName = CrossAssemblyWorkflowConstants.CanonicalName, Version = "2")]
public sealed class CrossAppWorkflowV2 : Workflow<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
    {
        return Task.FromResult($"v2:{input}");
    }
}

// Regression coverage for https://github.com/dapr/dotnet-sdk/issues/1859
//
// A public abstract open-generic workflow base class in a referenced assembly
// must NOT be emitted into the consuming assembly's generated versioning
// registry (it would produce CS0246 references to the unbound type parameters).
// Only the concrete closed derivative is registered. The consuming project
// (DaprWorkflowVersioningScanReferences=true) compiling is the regression test.

public static class CrossAssemblyGenericBaseWorkflowConstants
{
    public const string CanonicalName = "CrossAppGenericWorkflow";
}

public abstract class GenericBaseWorkflow<TInput, TOutput> : Workflow<TInput, TOutput>
{
}

[WorkflowVersion(CanonicalName = CrossAssemblyGenericBaseWorkflowConstants.CanonicalName, Version = "1")]
public sealed class ConcreteCrossAppGenericWorkflow : GenericBaseWorkflow<string, string>
{
    public override Task<string> RunAsync(WorkflowContext context, string input)
    {
        return Task.FromResult($"generic:{input}");
    }
}
