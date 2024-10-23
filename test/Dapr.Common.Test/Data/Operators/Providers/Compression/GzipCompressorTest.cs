// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Dapr.Common.Data.Operations.Providers.Compression;

namespace Dapr.Common.Test.Data.Operators.Providers.Compression;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class GzipCompressorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCompressData()
    {
        // Arrange
        var compressor = new GzipCompressor();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await compressor.ExecuteAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(input, result.Payload);
    }

    [Fact]
    public async Task ReverseAsync_ShouldDecompressData()
    {
        // Arrange
        var compressor = new GzipCompressor();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var compressedResult = await compressor.ExecuteAsync(input);

        // Act
        var result = await compressor.ReverseAsync(compressedResult, string.Empty, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input.ToArray(), result.Payload.ToArray());
    }
}

