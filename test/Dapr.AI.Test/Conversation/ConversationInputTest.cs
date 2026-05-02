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

namespace Dapr.AI.Test.Conversation;

public class ConversationInputTest
{
    [Fact]
    public void ConversationInput_WithMessages_ShouldSetMessages()
    {
        var messages = new IConversationMessage[]
        {
            new UserMessage { Content = [new MessageContent("Hello")] }
        };
        var input = new ConversationInput(messages);
        Assert.Equal(messages, input.Messages);
    }

    [Fact]
    public void ConversationInput_DefaultScrubPII_ShouldBeNull()
    {
        var input = new ConversationInput([]);
        Assert.Null(input.ScrubPII);
    }

    [Fact]
    public void ConversationInput_WithScrubPIITrue_ShouldSetTrue()
    {
        var input = new ConversationInput([], ScrubPII: true);
        Assert.True(input.ScrubPII);
    }

    [Fact]
    public void ConversationInput_WithScrubPIIFalse_ShouldSetFalse()
    {
        var input = new ConversationInput([], ScrubPII: false);
        Assert.False(input.ScrubPII);
    }

    [Fact]
    public void ConversationInput_WithEmptyMessages_ShouldWork()
    {
        var input = new ConversationInput([]);
        Assert.Empty(input.Messages);
    }

    [Fact]
    public void ConversationInput_WithMultipleMessages_ShouldPreserveOrder()
    {
        var messages = new IConversationMessage[]
        {
            new SystemMessage { Content = [new MessageContent("System instruction")] },
            new UserMessage { Content = [new MessageContent("User query")] },
            new AssistantMessage { Content = [new MessageContent("Assistant reply")] }
        };
        var input = new ConversationInput(messages);

        Assert.Equal(3, input.Messages.Count);
        Assert.IsType<SystemMessage>(input.Messages[0]);
        Assert.IsType<UserMessage>(input.Messages[1]);
        Assert.IsType<AssistantMessage>(input.Messages[2]);
    }

    [Fact]
    public void ConversationInput_Equality_SameValuesShouldBeEqual()
    {
        var messages = new IConversationMessage[] { new UserMessage() };
        var input1 = new ConversationInput(messages, true);
        var input2 = new ConversationInput(messages, true);
        Assert.Equal(input1, input2);
    }

    [Fact]
    public void ConversationInput_Equality_DifferentScrubPIIShouldNotBeEqual()
    {
        var messages = new IConversationMessage[] { new UserMessage() };
        var input1 = new ConversationInput(messages, true);
        var input2 = new ConversationInput(messages, false);
        Assert.NotEqual(input1, input2);
    }
}
