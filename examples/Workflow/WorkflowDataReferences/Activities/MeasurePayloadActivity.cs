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

using System.Security.Cryptography;
using Dapr.Workflow;
using Dapr.Workflow.Payloads;

namespace WorkflowDataReferences.Activities;

/// <summary>
/// Streams a document's stored representation through a bounded buffer using an explicit data reference.
/// </summary>
/// <param name="resolver">The resolver registered by the host's opt-in <c>WithBinaryStorePayloads</c> call.</param>
/// <remarks>
/// <see cref="IWorkflowPayloadResolver.OpenAsync"/> returns raw serialized bytes, not a deserialized
/// document or just its content field, and the caller owns the stream. Unlike buffered resolution,
/// this path leaves integrity verification to the caller. This example stores untransformed JSON,
/// so hashing the streamed bytes can be compared directly with the reference's content hash.
/// </remarks>
internal sealed class MeasurePayloadActivity(IWorkflowPayloadResolver resolver)
    : WorkflowActivity<PayloadReference<DocumentPayload>, PayloadSummary>
{
    public override async Task<PayloadSummary> RunAsync(
        WorkflowActivityContext context, PayloadReference<DocumentPayload> input)
    {
        await using var stream = await resolver.OpenAsync(input);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[16 * 1024];
        long byteCount = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) != 0)
        {
            byteCount += read;
            hash.AppendData(buffer, 0, read);
        }

        var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(input.ContentHash, actualHash, StringComparison.Ordinal))
            throw new PayloadIntegrityException(input, actualHash);
        if (byteCount != input.Size)
            throw new InvalidOperationException($"Expected {input.Size} stored bytes, but read {byteCount}.");

        return new PayloadSummary(byteCount, actualHash);
    }
}
