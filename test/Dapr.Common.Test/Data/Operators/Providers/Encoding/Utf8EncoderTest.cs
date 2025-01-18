using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Data.Operations.Providers.Encoding;
using Xunit;

namespace Dapr.Common.Test.Data.Operators.Providers.Encoding;

public class Utf8EncoderTest
{
    [Fact]
    public async Task ExecuteAsync_ShouldEncodeData()
    {
        // Arrange
        var encoder = new Utf8Encoder();
        const string input = "This is a test value!";
        
        // Act
        var encodedResult = await encoder.ExecuteAsync(input);
        
        // Assert
        Assert.NotNull(encodedResult);
        Assert.Equal("VGhpcyBpcyBhIHRlc3QgdmFsdWUh", Convert.ToBase64String(encodedResult.Payload.Span));
    }

    [Fact]
    public async Task ReverseAsync_ShouldDecodeData()
    {
        // Arrange
        var encoder = new Utf8Encoder();
        const string input = "This is a test value!";
        var encodedResult = await encoder.ExecuteAsync(input);
        
        // Act 
        var reverseResult = await encoder.ReverseAsync(encodedResult, string.Empty, CancellationToken.None);
        
        // Assert
        Assert.NotNull(reverseResult);
        Assert.Equal(input, reverseResult.Payload);
    }
}
