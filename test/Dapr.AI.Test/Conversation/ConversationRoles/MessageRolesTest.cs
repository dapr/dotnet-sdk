// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;

namespace Dapr.AI.Test.Conversation.ConversationRoles;

public class MessageRolesTest
{
    #region SystemMessage

    [Fact]
    public void SystemMessage_Role_ShouldBeSystem()
    {
        var message = new SystemMessage();
        Assert.Equal(MessageRole.System, message.Role);
    }

    [Fact]
    public void SystemMessage_DefaultContent_ShouldBeEmpty()
    {
        var message = new SystemMessage();
        Assert.Empty(message.Content);
    }

    [Fact]
    public void SystemMessage_DefaultName_ShouldBeNull()
    {
        var message = new SystemMessage();
        Assert.Null(message.Name);
    }

    [Fact]
    public void SystemMessage_WithName_ShouldSetName()
    {
        var message = new SystemMessage { Name = "system-v1" };
        Assert.Equal("system-v1", message.Name);
    }

    [Fact]
    public void SystemMessage_WithContent_ShouldSetContent()
    {
        var content = new[] { new MessageContent("You are a helpful assistant.") };
        var message = new SystemMessage { Content = content };

        Assert.Single(message.Content);
        Assert.Equal("You are a helpful assistant.", message.Content[0].Text);
    }

    [Fact]
    public void SystemMessage_ImplementsIConversationMessage()
    {
        IConversationMessage message = new SystemMessage();
        Assert.Equal(MessageRole.System, message.Role);
    }

    #endregion

    #region UserMessage

    [Fact]
    public void UserMessage_Role_ShouldBeUser()
    {
        var message = new UserMessage();
        Assert.Equal(MessageRole.User, message.Role);
    }

    [Fact]
    public void UserMessage_DefaultContent_ShouldBeEmpty()
    {
        var message = new UserMessage();
        Assert.Empty(message.Content);
    }

    [Fact]
    public void UserMessage_DefaultName_ShouldBeNull()
    {
        var message = new UserMessage();
        Assert.Null(message.Name);
    }

    [Fact]
    public void UserMessage_WithName_ShouldSetName()
    {
        var message = new UserMessage { Name = "alice" };
        Assert.Equal("alice", message.Name);
    }

    [Fact]
    public void UserMessage_WithMultipleContentItems_ShouldSetAll()
    {
        var content = new[]
        {
            new MessageContent("Hello"),
            new MessageContent("World")
        };
        var message = new UserMessage { Content = content };

        Assert.Equal(2, message.Content.Count);
        Assert.Equal("Hello", message.Content[0].Text);
        Assert.Equal("World", message.Content[1].Text);
    }

    [Fact]
    public void UserMessage_ImplementsIConversationMessage()
    {
        IConversationMessage message = new UserMessage();
        Assert.Equal(MessageRole.User, message.Role);
    }

    #endregion

    #region AssistantMessage

    [Fact]
    public void AssistantMessage_Role_ShouldBeAssistant()
    {
        var message = new AssistantMessage();
        Assert.Equal(MessageRole.Assistant, message.Role);
    }

    [Fact]
    public void AssistantMessage_DefaultContent_ShouldBeEmpty()
    {
        var message = new AssistantMessage();
        Assert.Empty(message.Content);
    }

    [Fact]
    public void AssistantMessage_DefaultName_ShouldBeNull()
    {
        var message = new AssistantMessage();
        Assert.Null(message.Name);
    }

    [Fact]
    public void AssistantMessage_DefaultToolCalls_ShouldBeEmpty()
    {
        var message = new AssistantMessage();
        Assert.Empty(message.ToolCalls);
    }

    [Fact]
    public void AssistantMessage_WithContent_ShouldSetContent()
    {
        var message = new AssistantMessage
        {
            Content = [new MessageContent("I can help with that.")]
        };
        Assert.Single(message.Content);
        Assert.Equal("I can help with that.", message.Content[0].Text);
    }

    [Fact]
    public void AssistantMessage_WithToolCalls_ShouldSetToolCalls()
    {
        var toolCall = new CalledToolFunction("get_weather", "{\"city\": \"NYC\"}") { Id = "call-1" };
        var message = new AssistantMessage { ToolCalls = [toolCall] };

        Assert.Single(message.ToolCalls);
        var calledFunc = Assert.IsType<CalledToolFunction>(message.ToolCalls[0]);
        Assert.Equal("call-1", calledFunc.Id);
        Assert.Equal("get_weather", calledFunc.Name);
    }

    [Fact]
    public void AssistantMessage_ImplementsIConversationMessage()
    {
        IConversationMessage message = new AssistantMessage();
        Assert.Equal(MessageRole.Assistant, message.Role);
    }

    #endregion

    #region DeveloperMessage

    [Fact]
    public void DeveloperMessage_Role_ShouldBeDeveloper()
    {
        var message = new DeveloperMessage();
        Assert.Equal(MessageRole.Developer, message.Role);
    }

    [Fact]
    public void DeveloperMessage_DefaultContent_ShouldBeEmpty()
    {
        var message = new DeveloperMessage();
        Assert.Empty(message.Content);
    }

    [Fact]
    public void DeveloperMessage_DefaultName_ShouldBeNull()
    {
        var message = new DeveloperMessage();
        Assert.Null(message.Name);
    }

    [Fact]
    public void DeveloperMessage_WithName_ShouldSetName()
    {
        var message = new DeveloperMessage { Name = "dev-team" };
        Assert.Equal("dev-team", message.Name);
    }

    [Fact]
    public void DeveloperMessage_WithContent_ShouldSetContent()
    {
        var message = new DeveloperMessage
        {
            Content = [new MessageContent("Internal instructions")]
        };
        Assert.Single(message.Content);
        Assert.Equal("Internal instructions", message.Content[0].Text);
    }

    [Fact]
    public void DeveloperMessage_ImplementsIConversationMessage()
    {
        IConversationMessage message = new DeveloperMessage();
        Assert.Equal(MessageRole.Developer, message.Role);
    }

    #endregion

    #region ToolMessage

    [Fact]
    public void ToolMessage_Role_ShouldBeTool()
    {
        var message = new ToolMessage { Name = "my_tool" };
        Assert.Equal(MessageRole.Tool, message.Role);
    }

    [Fact]
    public void ToolMessage_DefaultContent_ShouldBeEmpty()
    {
        var message = new ToolMessage { Name = "my_tool" };
        Assert.Empty(message.Content);
    }

    [Fact]
    public void ToolMessage_DefaultId_ShouldBeNull()
    {
        var message = new ToolMessage { Name = "my_tool" };
        Assert.Null(message.Id);
    }

    [Fact]
    public void ToolMessage_Name_ShouldBeSet()
    {
        var message = new ToolMessage { Name = "weather_api" };
        Assert.Equal("weather_api", message.Name);
    }

    [Fact]
    public void ToolMessage_WithId_ShouldSetId()
    {
        var message = new ToolMessage { Name = "my_tool", Id = "tool-call-456" };
        Assert.Equal("tool-call-456", message.Id);
    }

    [Fact]
    public void ToolMessage_WithContent_ShouldSetContent()
    {
        var message = new ToolMessage
        {
            Name = "my_tool",
            Content = [new MessageContent("The weather is sunny.")]
        };
        Assert.Single(message.Content);
        Assert.Equal("The weather is sunny.", message.Content[0].Text);
    }

    [Fact]
    public void ToolMessage_ImplementsIConversationMessage()
    {
        IConversationMessage message = new ToolMessage { Name = "my_tool" };
        Assert.Equal(MessageRole.Tool, message.Role);
    }

    #endregion

    #region MessageContent

    [Fact]
    public void MessageContent_ShouldSetText()
    {
        var content = new MessageContent("Hello, world!");
        Assert.Equal("Hello, world!", content.Text);
    }

    [Fact]
    public void MessageContent_Equality_SameTextShouldBeEqual()
    {
        var content1 = new MessageContent("Hello");
        var content2 = new MessageContent("Hello");
        Assert.Equal(content1, content2);
    }

    [Fact]
    public void MessageContent_Equality_DifferentTextShouldNotBeEqual()
    {
        var content1 = new MessageContent("Hello");
        var content2 = new MessageContent("World");
        Assert.NotEqual(content1, content2);
    }

    #endregion
}
