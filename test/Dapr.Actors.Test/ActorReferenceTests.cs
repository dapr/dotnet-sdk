using Dapr.Actors.Client;
using Dapr.Actors.Test;
using Xunit;

namespace Dapr.Actors
{
    public class ActorReferenceTests
    {
        [Fact]
        public void GetActorReference_WhenActorIsNull_ReturnsNull()
        {
            // Arrange
            object actor = null;

            // Act
            var result = ActorReference.Get(actor);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetActorReference_FromActorProxy_ReturnsActorReference()
        {
            // Arrange
            var expectedActorId = new ActorId("abc");
            var expectedActorType = "TestActor";
            var proxy = ActorProxy.Create(expectedActorId, typeof(ITestActor), expectedActorType);

            // Act
            var actorReference = ActorReference.Get(proxy);

            // Assert
            Assert.NotNull(actorReference);
            Assert.Equal(expectedActorId, actorReference.ActorId);
            Assert.Equal(expectedActorType, actorReference.ActorType);
        }



    }
}
