using Dapr.AI.Conversation;

namespace Dapr.AI.Test.Conversation;

public class DaprConversationClientBuilderTest
{
    [Fact]
    public void Build_WithDefaultConfiguration_ShouldReturnNewInstanceOfDaprConversationClient()
    {
        // Arrange
        var conversationClientBuilder = new DaprConversationClientBuilder();

        // Act
        var client = conversationClientBuilder.Build();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<DaprConversationClient>(client);
    }
}
