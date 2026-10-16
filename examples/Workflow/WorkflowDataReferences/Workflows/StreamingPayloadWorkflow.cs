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
/// Passes a typed data reference to an activity without materializing the referenced document.
/// </summary>
/// <remarks>
/// The client creates the reference using <c>IWorkflowPayloadResolver.StoreAsync</c> before
/// scheduling this workflow. Declaring the input as <see cref="PayloadReference{T}"/> opts into
/// receiving the reference itself instead of automatic deserialization. It identifies immutable
/// stored data by store, key, size, and hash; its type argument describes the serialized value.
/// The activity performs streaming I/O, keeping this orchestration deterministic.
/// Referenced data must remain available for as long as retained histories or consumers need it.
/// </remarks>
internal sealed class StreamingPayloadWorkflow : Workflow<PayloadReference<DocumentPayload>, PayloadSummary>
{
    public override Task<PayloadSummary> RunAsync(WorkflowContext context, PayloadReference<DocumentPayload> input)
        => context.CallActivityAsync<PayloadSummary>(nameof(MeasurePayloadActivity), input);
}
