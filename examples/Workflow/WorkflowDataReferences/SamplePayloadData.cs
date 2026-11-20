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

/// <summary>Provides bounded-memory fixtures and explicit verification outside the introductory API walkthroughs.</summary>
internal static class SamplePayloadData
{
    /// <summary>The size of each generated raw-byte page.</summary>
    internal const int PageSize = 64 * 1024;
    /// <summary>The page count used by the 32 MiB direct-upload recipe.</summary>
    internal const int PageCount = 512;

    /// <summary>Simulates a paginated external byte source using one reusable buffer.</summary>
    /// <param name="pageCount">The nonnegative number of pages to generate.</param>
    /// <param name="cancellationToken">Cancels generation between pages.</param>
    /// <returns>Raw byte chunks that must be consumed before advancing the enumeration.</returns>
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

    /// <summary>Consumes a full download and checks its length and SHA-256 against both the fixture and reference.</summary>
    /// <remarks>This is sample verification, not automatic integrity checking by the SDK's raw-byte reader.</remarks>
    /// <param name="chunks">The download sequence, already configured with the caller's cancellation token.</param>
    /// <param name="reference">The expected reference metadata.</param>
    /// <param name="pageCount">The number of fixture pages originally uploaded.</param>
    /// <returns>A task that completes after all bytes have been verified.</returns>
    /// <exception cref="InvalidOperationException">The received bytes or reference metadata do not match the fixture.</exception>
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
