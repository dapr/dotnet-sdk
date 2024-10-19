using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Data.Operations.Providers.Masking;
using Xunit;

namespace Dapr.Common.Test.Data.Operators.Providers.Masking;

public class DaprRegularExpressionMapperTest
{
    [Fact]
    public async Task ExecuteAsync_ShouldRunMapperCorrectly()
    {
        //Arrange
        var mapper = new DaprRegularExpressionMasker();
        const string input = "This is not a real social security number: 012-34-5678";
        mapper.RegisterMatch(new Regex(@"\d{3}-\d{2}-\d{4}"), "***-**-****");
        
        //Act
        var result = await mapper.ExecuteAsync(input);

        //Assert
        Assert.Equal("This is not a real social security number: ***-**-****", result.Payload);
        Assert.True(result.Metadata.ContainsKey("Ops"));
        Assert.Equal(mapper.Name, result.Metadata["Ops"]);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowIsCancellationTokenCancelled()
    {
        //Arrange
        var mapper = new DaprRegularExpressionMasker();
        const string input = "This is not a real social security number: 012-34-5678";
        mapper.RegisterMatch(new Regex(@"\d{3}-\d{2}-\d{4}"), "***-**-****");
        
        //Act
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); //CancelAsync is only in .NET 8 and up

        //Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => mapper.ExecuteAsync(input, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ReverseAsync_ShouldNotDoAnythingToPayload()
    {
        //Arrange
        var mapper = new DaprRegularExpressionMasker();
        const string input = "This is a date: 2/18/2026";
        mapper.RegisterMatch(new Regex(@"\d{3}-\d{2}-\d{4}"), "***-**-****");
        
        //Act
        var result = await mapper.ExecuteAsync(input);
        var reverseResult = await mapper.ReverseAsync(result);
        
        //Assert
        Assert.Equal(input, reverseResult.Payload);
    }
}
