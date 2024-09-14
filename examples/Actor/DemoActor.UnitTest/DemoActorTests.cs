using Dapr.Actors.Runtime;
using DaprDemoActor;
using IDemoActorInterface;
using Moq;

namespace DemoActor.UnitTest
{
    public class DemoActorTests
    {
        [Fact]
        public async Task SaveData_CorrectlyPersistDataWithGiveTTL()
        {
            // arrange
            // Name of the state to be saved in the actor
            var actorStateName = "my_data";
            // Create a mock actor state manager to simulate the actor state
            var mockStateManager = new Mock<IActorStateManager>(MockBehavior.Strict);
            // Prepare other dependencies
            var bankService = new BankService();
            // Create an actor host for testing
            var host = ActorHost.CreateForTest<DaprDemoActor.DemoActor>();
            // Create an actor instance with the mock state manager and its dependencies
            var storageActor = new DaprDemoActor.DemoActor(host, bankService, mockStateManager.Object);
            // Prepare test data to be saved
            var data = new MyDataWithTTL
            {
                MyData = new MyData
                {
                    PropertyA = "PropA",
                    PropertyB = "PropB",
                },
                TTL = TimeSpan.FromSeconds(10)
            };
            // Setup the mock state manager to enable the actor to save the state with the SetStateAsync method, and return
            //   a completed task when the state is saved, so that the actor can continue with the test.
            // When MockBehavior.Strict is used, the test will fail if the actor does not call SetStateAsync or 
            //   calls other methods on the state manager.
            mockStateManager
                .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<MyData>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // act
            await storageActor.SaveData(data);

            // assert
            // Verify that the state manager is called with the correct state name and data, only one time.
            mockStateManager.Verify(x => x.SetStateAsync(
                actorStateName,
                It.Is<MyData>(x => x.PropertyA == "PropA" && x.PropertyB == "PropB"),
                It.Is<TimeSpan>(x => x.TotalSeconds == 10),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetData_CorrectlyRetrieveData()
        {
            // arrange
            // Name of the state to be saved in the actor
            var actorStateName = "my_data";
            // Create a mock actor state manager to simulate the actor state
            var mockStateManager = new Mock<IActorStateManager>(MockBehavior.Strict);
            // Prepare other dependencies
            var bankService = new BankService();
            // Create an actor host for testing
            var host = ActorHost.CreateForTest<DaprDemoActor.DemoActor>();
            // Create an actor instance with the mock state manager and its dependencies
            var storageActor = new DaprDemoActor.DemoActor(host, bankService, mockStateManager.Object);
            // Prepare prepare the state to be returned by the state manager
            var state = new MyData
            {
                PropertyA = "PropA",
                PropertyB = "PropB",
            };
            // Setup the mock state manager to return the state when the actor calls GetStateAsync.
            mockStateManager
                .Setup(x => x.GetStateAsync<MyData>(actorStateName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(state));

            // act
            var result = await storageActor.GetData();

            // assert
            // Verify that the state manager is called with the correct state name, only one time.
            mockStateManager.Verify(x => x.GetStateAsync<MyData>(actorStateName, It.IsAny<CancellationToken>()), Times.Once);
            // Verify that the actor returns the correct data.
            Assert.Equal("PropA", result.PropertyA);
            Assert.Equal("PropB", result.PropertyB);
        }
    }
}
