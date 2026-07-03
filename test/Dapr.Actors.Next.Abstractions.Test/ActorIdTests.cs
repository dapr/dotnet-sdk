namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class ActorIdTests
{
    [Fact]
    public void Create_StoresValueAndFormatsAsValue()
    {
        var id = ActorId.Create("cart-1");

        Assert.Equal("cart-1", id.Value);
        Assert.Equal("cart-1", id.ToString());
    }

    [Fact]
    public void Parse_ReturnsEquivalentActorId()
    {
        Assert.Equal(new ActorId("cart-1"), ActorId.Parse("cart-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsInvalidValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new ActorId(value));
    }

    [Fact]
    public void Constructor_RejectsNull()
    {
        Assert.Throws<ArgumentException>(() => new ActorId(null!));
    }

    [Fact]
    public void TryParse_ReturnsTrueForValidValue()
    {
        var parsed = ActorId.TryParse("actor-7", out var id);

        Assert.True(parsed);
        Assert.Equal("actor-7", id.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryParse_ReturnsFalseForInvalidValue(string? value)
    {
        var parsed = ActorId.TryParse(value, out var id);

        Assert.False(parsed);
        Assert.Equal(default, id);
    }

    [Fact]
    public void Equality_UsesValue()
    {
        Assert.Equal(ActorId.Create("same"), ActorId.Parse("same"));
        Assert.NotEqual(ActorId.Create("left"), ActorId.Create("right"));
    }
}
