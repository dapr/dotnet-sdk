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

/// <summary>Forwards the activity's completed upload reference without reading the data.</summary>
internal sealed class DirectWritePayloadWorkflow : Workflow<int, PayloadReference>
{
    /// <inheritdoc />
    /// <param name="context">The workflow context used to schedule the upload activity.</param>
    /// <param name="pageCount">The number of pages the activity should produce.</param>
    public override Task<PayloadReference> RunAsync(WorkflowContext context, int pageCount)
        => context.CallActivityAsync<PayloadReference>(nameof(WritePagedPayloadActivity), pageCount);
}
