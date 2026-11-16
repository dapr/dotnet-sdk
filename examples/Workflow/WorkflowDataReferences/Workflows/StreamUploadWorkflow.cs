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
using Dapr.Workflow.Payloads;
using WorkflowDataReferences.Activities;

namespace WorkflowDataReferences.Workflows;

/// <summary>
/// Passes raw binary payload references through workflow and activity inputs and outputs.
/// All uploads happen in the client or activity, never in the replayed workflow.
/// </summary>
internal sealed class StreamUploadWorkflow : Workflow<PayloadReference, PayloadReference>
{
    /// <inheritdoc />
    public override Task<PayloadReference> RunAsync(WorkflowContext context, PayloadReference input)
    {
        // All four boundaries carry references: workflow input -> activity input ->
        // activity output -> workflow output. No live Stream enters orchestration history.
        // The activity performs the write; returning its reference here neither uploads nor downloads.
        return context.CallActivityAsync<PayloadReference>(nameof(CopyStreamPayloadActivity), input);
    }
}
