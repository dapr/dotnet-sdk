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

using Microsoft.Extensions.Options;

namespace Dapr.Workflow.BinaryStore;

/// <summary>
/// Attaches a per-execution <see cref="IWorkflowPayloadResolver"/> feature to every orchestrator
/// invocation so that <see cref="Extensions.WorkflowContextBinaryStoreExtensions"/> can locate it.
/// </summary>
/// <param name="resolver">The application-wide resolver to wrap per execution.</param>
/// <param name="options">The offloading options, used for the cache size.</param>
internal sealed class BinaryStorePayloadFeatureProvider(
    IWorkflowPayloadResolver resolver,
    IOptions<BinaryStorePayloadOptions> options) : IWorkflowFeatureProvider
{
    private readonly IWorkflowPayloadResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    private readonly BinaryStorePayloadOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public void Configure(string instanceId, IWorkflowFeatureRegistry registry)
        => registry.Set<IWorkflowPayloadResolver>(
            new CachingWorkflowPayloadResolver(_resolver, _options.ResolutionCacheSize));
}
