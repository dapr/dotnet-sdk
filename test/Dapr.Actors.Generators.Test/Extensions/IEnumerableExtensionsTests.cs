using Dapr.Actors.Generators.Extensions;

namespace Dapr.Actors.Generators.Test.Extensions;

public class IEnumerableExtensionsTests
{
    [Fact]
    public void IndexOf_WhenPredicateIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var source = new[] { 1, 2, 3, 4, 5 };
        Func<int, bool> predicate = null!;

        // Act
        Action act = () => source.IndexOf(predicate);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(new int[] { }, 3, -1)]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 6, -1)]
    public void IndexOf_WhenItemDoesNotExist_ReturnsMinusOne(int[] source, int item, int expected)
    {
        // Arrange
        Func<int, bool> predicate = (x) => x == item;

        // Act
        var index = source.IndexOf(predicate);

        // Assert
        Assert.Equal(expected, index);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 3, 2)]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 1, 0)]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 5, 4)]
    public void IndexOf_WhenItemExists_ReturnsIndexOfItem(int[] source, int item, int expected)
    {
        // Arrange
        Func<int, bool> predicate = (x) => x == item;

        // Act
        var index = source.IndexOf(predicate);

        // Assert
        Assert.Equal(expected, index);
    }
}