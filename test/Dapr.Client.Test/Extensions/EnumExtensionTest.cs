using System.Runtime.Serialization;
using Xunit;

namespace Dapr.Client.Test.Extensions;

public class EnumExtensionTest
{
    [Fact]
    public void GetValueFromEnumMember_RedResolvesAsExpected()
    {
        var value = TestEnum.Red.GetValueFromEnumMember();
        Assert.Equal("red", value);
    }

    [Fact]
    public void GetValueFromEnumMember_YellowResolvesAsExpected()
    {
        var value = TestEnum.Yellow.GetValueFromEnumMember();
        Assert.Equal("YELLOW", value);
    }

    [Fact]
    public void GetValueFromEnumMember_BlueResolvesAsExpected()
    {
        var value = TestEnum.Blue.GetValueFromEnumMember();
        Assert.Equal("Blue", value);
    }
}

public enum TestEnum
{
    [EnumMember(Value = "red")]
    Red,
    [EnumMember(Value = "YELLOW")]
    Yellow,
    Blue
}