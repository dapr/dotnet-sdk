#nullable enable
using System;
using System.Runtime.Serialization;
using Dapr.Common.Extensions;
using Xunit;

namespace Dapr.Common.Test.Extensions;

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

    // TryParseEnumMember<TEnum> tests

    [Theory]
    [InlineData("rouge", TestColor.Red)]
    [InlineData("ROUGE", TestColor.Red)] // case-insensitive EnumMember match
    [InlineData("blue", TestColor.Blue)]
    [InlineData("BLUE", TestColor.Blue)] // case-insensitive EnumMember match
    [InlineData("Bright-Red", TestColor.BrightRed)]
    [InlineData("bright-red", TestColor.BrightRed)] // case-insensitive EnumMember match
    public void TryParseEnumMember_Matches_EnumMember_Value(string input, TestColor expected)
    {
        var ok = input.TryParseEnumMember<TestColor>(out var actual);

        Assert.True(ok);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Green", TestColor.Green)]
    [InlineData("green", TestColor.Green)] // case-insensitive name fallback
    public void TryParseEnumMember_Fallbacks_To_Enum_Name(string input, TestColor expected)
    {
        var ok = input.TryParseEnumMember<TestColor>(out var actual);

        Assert.True(ok);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseEnumMember_ReturnsFalse_For_NullOrWhitespace(string? input)
    {
        var ok = input.TryParseEnumMember<TestColor>(out var actual);

        Assert.False(ok);
        Assert.Equal(default, actual);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("roug")] // close but not exact
    public void TryParseEnumMember_ReturnsFalse_For_Invalid_Value(string input)
    {
        var ok = input.TryParseEnumMember<TestColor>(out var actual);

        Assert.False(ok);
        Assert.Equal(default, actual);
    }

    // ParseEnumMember<TEnum> tests

    [Theory]
    [InlineData("rouge", TestColor.Red)]
    [InlineData("ROUGE", TestColor.Red)]
    [InlineData("blue", TestColor.Blue)]
    [InlineData("BLUE", TestColor.Blue)]
    [InlineData("green", TestColor.Green)] // fallback to name
    [InlineData("Bright-Red", TestColor.BrightRed)]
    public void ParseEnumMember_Returns_Parsed_Value(string input, TestColor expected)
    {
        var actual = EnumExtensions.ParseEnumMember<TestColor>(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("  ")]
    [InlineData("")]
    public void ParseEnumMember_Throws_For_Invalid_Value(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            EnumExtensions.ParseEnumMember<TestColor>(input));

        Assert.Equal("value", ex.ParamName);
        Assert.Contains($"Cannot map '{input}' to {nameof(TestColor)} via EnumMember or name.", ex.Message);
    }
}

public enum TestColor
{
    [EnumMember(Value = "rouge")] 
    Red,

    // No EnumMemberAttribute to test fallback to enum identifier
    Green,

    [EnumMember(Value = "blue")] 
    Blue,

    [EnumMember(Value = "Bright-Red")] 
    BrightRed,
}

public enum TestEnum
{
    [EnumMember(Value = "red")]
    Red,
    [EnumMember(Value = "YELLOW")]
    Yellow,
    Blue
}
