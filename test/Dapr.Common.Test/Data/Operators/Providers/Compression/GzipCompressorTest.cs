using Dapr.Common.Data.Operations.Providers.Compression.Gzip;

namespace Dapr.Common.Test.Data.Operators.Providers.Compression;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class DaprGzipCompressorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCompressData()
    {
        // Arrange
        var compressor = new DaprGzipCompressor();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await compressor.ExecuteAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(input, result.Payload);
        Assert.True(result.Metadata.ContainsKey("Ops"));
        Assert.Equal(compressor.Name, result.Metadata["Ops"]);
    }

    [Fact]
    public async Task ReverseAsync_ShouldDecompressData()
    {
        // Arrange
        var compressor = new DaprGzipCompressor();
        var input = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var compressedResult = await compressor.ExecuteAsync(input);

        // Act
        var result = await compressor.ReverseAsync(compressedResult, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input.ToArray(), result.Payload.ToArray());
    }
}

