using System.Threading.Tasks;
using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Xunit;

namespace Dapr.Common.Test.Data.Operators.Providers.Serialization;

public class SystemTextJsonSerializerTest
{
    [Fact]
    public async Task ExecuteAsync_ShouldSerialize()
    {
        //Arrange
        var validator = new SystemTextJsonSerializer<TestObject>();
        var input = new TestObject("Test", 15);
        
        //Act
        var result = await validator.ExecuteAsync(input);
        
        //Assert
        Assert.NotNull(result);
        Assert.Equal("{\"name\":\"Test\",\"count\":15}", result.Payload);
    }

    [Fact]
    public async Task ReverseAsync_ShouldDeserialize()
    {
        //Arrange
        var validator = new SystemTextJsonSerializer<TestObject>();
        const string input = "{\"name\":\"Test\",\"count\":15}";
        var payload = new DaprOperationPayload<string>(input);
        
        //Act
        var result = await validator.ReverseAsync(payload);

        //Assert
        Assert.NotNull(result);
        var expected = new TestObject("Test", 15);
        Assert.Equal(expected, result.Payload);
    }

    private record TestObject(string Name, int Count);
}
