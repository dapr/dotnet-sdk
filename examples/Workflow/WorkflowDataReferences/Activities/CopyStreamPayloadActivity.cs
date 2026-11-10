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

/// <summary>
/// Stores a non-seekable download stream as an activity result without materializing its bytes.
/// Identical content in the same instance reuses the existing content-addressed key.
/// </summary>
internal sealed class CopyStreamPayloadActivity
    : WorkflowActivity<PayloadReference, PayloadReference>
{
    public override async Task<PayloadReference> RunAsync(WorkflowActivityContext context, PayloadReference input)
    {
        // OpenPayloadAsync exposes a non-seekable download. StorePayloadStreamAsync consumes it incrementally,
        // stages to temporary disk to calculate the content-addressed key, then uploads.
        // Replace the source with a generated/transformed stream to produce different output bytes.
        await using var content = await context.OpenPayloadAsync(input);
        var output = await context.StorePayloadStreamAsync(content);
        if (output.ContentHash != input.ContentHash)
            throw new PayloadIntegrityException(input, output.ContentHash);
        if (output.Size != input.Size)
            throw new InvalidOperationException($"Expected {input.Size} bytes, but copied {output.Size}.");
        // Return the reference, not the stream. The workflow can forward it without materializing bytes.
        return output;
    }
}
