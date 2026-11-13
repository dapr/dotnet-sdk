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
/// Explicitly reads payload chunks in the workflow, then asks an activity to independently read them.
/// </summary>
/// <remarks>
/// The context queues storage I/O outside deterministic execution. Enumeration reads storage again
/// on replay, so the referenced bytes must remain immutable and available. Only the byte total,
/// not chunk boundaries, influences the workflow. The activity also verifies the content hash.
/// </remarks>
internal sealed class ChunkStreamingWorkflow : Workflow<PayloadReference<DocumentPayload>, PayloadSummary>
{
    /// <inheritdoc />
    public override async Task<PayloadSummary> RunAsync(
        WorkflowContext context, PayloadReference<DocumentPayload> input)
    {
        long byteCount = 0;
        // Use the workflow context API, not an injected resolver/direct storage client.
        // The SDK schedules these reads safely, but it does not persist the chunks in history.
        // Replays read them again; prefer forwarding to an activity when the workflow needs only a summary.
        await foreach (var chunk in context.ReadPayloadChunksAsync(input, chunkSize: 32 * 1024))
        {
            byteCount += chunk.Length;
        }

        if (byteCount != input.Size)
            throw new InvalidOperationException($"Expected {input.Size} stored bytes, but the workflow read {byteCount}.");

        // The activity opens its own download with resolver.ReadChunksAsync(input).
        return await context.CallActivityAsync<PayloadSummary>(nameof(MeasurePayloadActivity), input);
    }
}
