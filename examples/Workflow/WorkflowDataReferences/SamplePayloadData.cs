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

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Dapr.Workflow.Payloads;

namespace WorkflowDataReferences;

internal static class SamplePayloadData
{
    internal const int PageSize = 64 * 1024;
    internal const int PageCount = 512;

    internal static async IAsyncEnumerable<ReadOnlyMemory<byte>> FetchPagesAsync(int pageCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        // Stand-in for a paginated external source. Upload consumes each page before this buffer is reused.
        var page = CreatePage();
        for (var index = 0; index < pageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return page;
        }
    }

    internal static async Task VerifyPagesAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks,
        PayloadReference reference, int pageCount)
    {
        var page = CreatePage();
        using var expected = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var index = 0; index < pageCount; index++)
            expected.AppendData(page);

        using var actual = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = 0;
        await foreach (var chunk in chunks)
        {
            actual.AppendData(chunk.Span);
            length += chunk.Length;
        }

        var expectedHash = Convert.ToHexString(expected.GetHashAndReset()).ToLowerInvariant();
        var actualHash = Convert.ToHexString(actual.GetHashAndReset()).ToLowerInvariant();
        if (length != (long)pageCount * PageSize || length != reference.Size ||
            actualHash != expectedHash || actualHash != reference.ContentHash)
            throw new InvalidOperationException("The directly uploaded bytes did not match the generated pages.");
    }

    private static byte[] CreatePage()
    {
        var page = new byte[PageSize];
        for (var index = 0; index < page.Length; index++)
            page[index] = (byte)(index % 251);
        return page;
    }
}
