using Dapr.Actors.Generators.Extensions;

namespace Dapr.Actors.Generators.Test.Extensions
{
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
    }
}
