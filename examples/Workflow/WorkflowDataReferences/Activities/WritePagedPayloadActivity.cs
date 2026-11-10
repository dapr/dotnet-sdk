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

namespace WorkflowDataReferences.Activities;

/// <summary>Uploads generated pages as they arrive, without local files or a whole-result buffer.</summary>
internal sealed class WritePagedPayloadActivity : WorkflowActivity<int, PayloadReference>
{
    /// <inheritdoc />
    /// <param name="context">The activity context, which supplies upload ownership, prefix metadata, and cancellation.</param>
    /// <param name="pageCount">The number of generated pages to upload without accumulating them in memory.</param>
    public override Task<PayloadReference> RunAsync(WorkflowActivityContext context, int pageCount)
        => context.UploadPayloadAsync(SamplePayloadData.FetchPagesAsync(pageCount, context.CancellationToken));
}
