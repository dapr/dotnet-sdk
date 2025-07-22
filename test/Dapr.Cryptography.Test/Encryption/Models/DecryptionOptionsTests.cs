using System;
using Dapr.Cryptography.Encryption.Models;

namespace Dapr.Cryptography.Test.Encryption.Models;

public class DecryptionOptionsTests
{
    [Fact]
    public void DefaultStreamingBlockSizeInBytes_Is4KB()
    {
        var options = new DecryptionOptions();
        var defaultBlockSize = options.StreamingBlockSizeInBytes;
        Assert.Equal(4 * 1024, defaultBlockSize);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(8192)]
    public void StreamingBlockSizeInBytes_SetValidValue_UpdatesBlockSize(int newBlockSize)
    {
        var options = new DecryptionOptions();
        options.StreamingBlockSizeInBytes = newBlockSize;
        Assert.Equal(newBlockSize, options.StreamingBlockSizeInBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1024)]
    public void StreamingBlockSizeInBytes_SetInvalidValue_ThrowsArgumentOutOfRangeException(int invalidBlockSize)
    {
        var options = new DecryptionOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.StreamingBlockSizeInBytes = invalidBlockSize);
    }
}
