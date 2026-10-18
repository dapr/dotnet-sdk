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

namespace Dapr.Workflow;

/// <summary>
/// Implemented by SDK services that contribute features to every workflow execution.
/// The runtime resolves all registered providers from DI and invokes them when building the
/// feature collection for an orchestrator invocation.
/// </summary>
internal interface IWorkflowFeatureProvider
{
    /// <summary>
    /// Adds features for the execution identified by <paramref name="instanceId"/>.
    /// </summary>
    /// <param name="instanceId">The workflow instance being executed.</param>
    /// <param name="registry">The registry to add features to.</param>
    void Configure(string instanceId, IWorkflowFeatureRegistry registry);
}
