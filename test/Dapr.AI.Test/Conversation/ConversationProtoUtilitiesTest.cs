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

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Tools;
using Dapr.Client.Autogen.Grpc.v1;
using DaprConversationInput = Dapr.AI.Conversation.ConversationInput;

namespace Dapr.AI.Test.Conversation;

public class ConversationProtoUtilitiesTest
{
    #region ToProtoContents

    [Fact]
    public void ToProtoContents_ShouldMapTextCorrectly()
    {
        var contents = new[]
        {
            new MessageContent("Hello"),
            new MessageContent("World")
        };

        var protoContents = contents.ToProtoContents().ToList();

        Assert.Equal(2, protoContents.Count);
        Assert.Equal("Hello", protoContents[0].Text);
        Assert.Equal("World", protoContents[1].Text);
    }

    [Fact]
    public void ToProtoContents_WithEmptyList_ShouldReturnEmpty()
    {
        var contents = new MessageContent[0];
        var protoContents = contents.ToProtoContents().ToList();
        Assert.Empty(protoContents);
    }

    [Fact]
    public void ToProtoContents_WithSingleItem_ShouldReturnSingleItem()
    {
        var contents = new[] { new MessageContent("Only one") };
        var protoContents = contents.ToProtoContents().ToList();

        Assert.Single(protoContents);
        Assert.Equal("Only one", protoContents[0].Text);
    }

    #endregion

    #region CreateConversationInputRequest

    [Fact]
    public void CreateConversationInputRequest_ShouldSetConversationComponentId()
    {
        var options = new ConversationOptions("my-llm-component");
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Equal("my-llm-component", request.Name);
    }

    [Fact]
    public void CreateConversationInputRequest_WithContextId_ShouldSetContextId()
    {
        var options = new ConversationOptions("component") { ContextId = "ctx-abc123" };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Equal("ctx-abc123", request.ContextId);
    }

    [Fact]
    public void CreateConversationInputRequest_WithoutContextId_ShouldNotSetContextId()
    {
        var options = new ConversationOptions("component");
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.True(string.IsNullOrEmpty(request.ContextId));
    }

    [Fact]
    public void CreateConversationInputRequest_WithToolChoiceNone_ShouldSetToolChoice()
    {
        var options = new ConversationOptions("component") { ToolChoice = ToolChoice.None };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Equal("none", request.ToolChoice);
    }

    [Fact]
    public void CreateConversationInputRequest_WithToolChoiceAuto_ShouldSetToolChoice()
    {
        var options = new ConversationOptions("component") { ToolChoice = ToolChoice.Auto };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Equal("auto", request.ToolChoice);
    }

    [Fact]
    public void CreateConversationInputRequest_WithToolChoiceRequired_ShouldSetToolChoice()
    {
        var options = new ConversationOptions("component") { ToolChoice = ToolChoice.Required };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Equal("required", request.ToolChoice);
    }

    [Fact]
    public void CreateConversationInputRequest_WithoutToolChoice_ShouldNotSetToolChoice()
    {
        var options = new ConversationOptions("component");
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.True(string.IsNullOrEmpty(request.ToolChoice));
    }

    [Fact]
    public void CreateConversationInputRequest_WithScrubPIITrue_ShouldSetScrubPII()
    {
        var options = new ConversationOptions("component") { ScrubPII = true };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.True(request.ScrubPii);
    }

    [Fact]
    public void CreateConversationInputRequest_WithScrubPIIFalse_ShouldSetScrubPIIFalse()
    {
        var options = new ConversationOptions("component") { ScrubPII = false };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.False(request.ScrubPii);
    }

    [Fact]
    public void CreateConversationInputRequest_WithTemperature_ShouldSetTemperature()
    {
        var options = new ConversationOptions("component") { Temperature = 0.85 };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Equal(0.85, request.Temperature, 0.001);
    }

    [Fact]
    public void CreateConversationInputRequest_WithMetadata_ShouldSetMetadata()
    {
        var options = new ConversationOptions("component")
        {
            Metadata = new Dictionary<string, string>
            {
                { "source", "test" },
                { "version", "1.0" }
            }
        };
        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Equal(2, request.Metadata.Count);
        Assert.Equal("test", request.Metadata["source"]);
        Assert.Equal("1.0", request.Metadata["version"]);
    }

    [Fact]
    public void CreateConversationInputRequest_WithUserMessageInput_ShouldMapToUserMessage()
    {
        var userMessage = new UserMessage { Content = [new MessageContent("Hello from user")] };
        var input = new DaprConversationInput([userMessage]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        Assert.Single(request.Inputs);
        Assert.Single(request.Inputs[0].Messages);
        Assert.NotNull(request.Inputs[0].Messages[0].OfUser);
        Assert.Equal("Hello from user", request.Inputs[0].Messages[0].OfUser.Content[0].Text);
    }

    [Fact]
    public void CreateConversationInputRequest_WithSystemMessageInput_ShouldMapToSystemMessage()
    {
        var systemMessage = new SystemMessage
        {
            Name = "sys",
            Content = [new MessageContent("You are a helpful assistant.")]
        };
        var input = new DaprConversationInput([systemMessage]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        Assert.Single(request.Inputs[0].Messages);
        Assert.NotNull(request.Inputs[0].Messages[0].OfSystem);
        Assert.Equal("sys", request.Inputs[0].Messages[0].OfSystem.Name);
        Assert.Equal("You are a helpful assistant.", request.Inputs[0].Messages[0].OfSystem.Content[0].Text);
    }

    [Fact]
    public void CreateConversationInputRequest_WithAssistantMessage_ShouldMapToAssistantMessage()
    {
        var assistantMessage = new AssistantMessage { Content = [new MessageContent("I can help.")] };
        var input = new DaprConversationInput([assistantMessage]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        Assert.NotNull(request.Inputs[0].Messages[0].OfAssistant);
        Assert.Equal("I can help.", request.Inputs[0].Messages[0].OfAssistant.Content[0].Text);
    }

    [Fact]
    public void CreateConversationInputRequest_WithDeveloperMessage_ShouldMapToDeveloperMessage()
    {
        var devMessage = new DeveloperMessage { Content = [new MessageContent("Internal info")] };
        var input = new DaprConversationInput([devMessage]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        Assert.NotNull(request.Inputs[0].Messages[0].OfDeveloper);
        Assert.Equal("Internal info", request.Inputs[0].Messages[0].OfDeveloper.Content[0].Text);
    }

    [Fact]
    public void CreateConversationInputRequest_WithToolMessage_ShouldMapToToolMessage()
    {
        var toolMessage = new ToolMessage
        {
            Name = "weather_api",
            Id = "call-999",
            Content = [new MessageContent("Sunny, 72°F")]
        };
        var input = new DaprConversationInput([toolMessage]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        var protoTool = request.Inputs[0].Messages[0].OfTool;
        Assert.NotNull(protoTool);
        Assert.Equal("weather_api", protoTool.Name);
        Assert.Equal("call-999", protoTool.ToolId);
        Assert.Equal("Sunny, 72°F", protoTool.Content[0].Text);
    }

    [Fact]
    public void CreateConversationInputRequest_WithAssistantToolCalls_ShouldMapToolCalls()
    {
        var toolCall = new CalledToolFunction("get_weather", "{\"city\":\"NYC\"}") { Id = "call-123" };
        var assistantMessage = new AssistantMessage { ToolCalls = [toolCall] };
        var input = new DaprConversationInput([assistantMessage]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        var protoAssistant = request.Inputs[0].Messages[0].OfAssistant;
        Assert.Single(protoAssistant.ToolCalls);
        Assert.Equal("call-123", protoAssistant.ToolCalls[0].Id);
        Assert.Equal("get_weather", protoAssistant.ToolCalls[0].Function.Name);
        Assert.Equal("{\"city\":\"NYC\"}", protoAssistant.ToolCalls[0].Function.Arguments);
    }

    [Fact]
    public void CreateConversationInputRequest_WithInputScrubPII_ShouldSetInputScrubPII()
    {
        var input = new DaprConversationInput([], ScrubPII: true);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        Assert.True(request.Inputs[0].ScrubPii);
    }

    [Fact]
    public void CreateConversationInputRequest_WithToolFunction_ShouldSetTools()
    {
        var tool = new ToolFunction("get_weather") { Description = "Gets current weather" };
        var options = new ConversationOptions("component") { Tools = [tool] };

        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Single(request.Tools);
        Assert.NotNull(request.Tools[0].Function);
        Assert.Equal("get_weather", request.Tools[0].Function.Name);
        Assert.Equal("Gets current weather", request.Tools[0].Function.Description);
    }

    [Fact]
    public void CreateConversationInputRequest_WithToolFunctionAndParameters_ShouldMapParameters()
    {
        var parameters = new Dictionary<string, object?> { { "city", "string" } };
        var tool = new ToolFunction("get_weather") { Parameters = parameters };
        var options = new ConversationOptions("component") { Tools = [tool] };

        var request = ConversationProtoUtilities.CreateConversationInputRequest([], options);

        Assert.Single(request.Tools);
        Assert.True(request.Tools[0].Function.Parameters.Fields.ContainsKey("city"));
    }

    [Fact]
    public void CreateConversationInputRequest_WithUnsupportedTool_ShouldThrowNotSupportedException()
    {
        var options = new ConversationOptions("component") { Tools = [new UnsupportedTool()] };

        Assert.Throws<NotSupportedException>(() =>
            ConversationProtoUtilities.CreateConversationInputRequest([], options));
    }

    [Fact]
    public void CreateConversationInputRequest_WithMultipleInputs_ShouldMapAll()
    {
        var input1 = new DaprConversationInput([new UserMessage { Content = [new MessageContent("First")] }]);
        var input2 = new DaprConversationInput([new UserMessage { Content = [new MessageContent("Second")] }]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input1, input2], options);

        Assert.Equal(2, request.Inputs.Count);
    }

    [Fact]
    public void CreateConversationInputRequest_WithUserMessageAndName_ShouldSetName()
    {
        var userMessage = new UserMessage { Name = "alice", Content = [new MessageContent("Hi")] };
        var input = new DaprConversationInput([userMessage]);
        var options = new ConversationOptions("component");

        var request = ConversationProtoUtilities.CreateConversationInputRequest([input], options);

        Assert.Equal("alice", request.Inputs[0].Messages[0].OfUser.Name);
    }

    #endregion

    #region ToDomain

    [Fact]
    public void ToDomain_ShouldMapConversationId()
    {
        var proto = new ConversationResponseAlpha2 { ContextId = "ctx-xyz" };
        var result = proto.ToDomain();

        Assert.Equal("ctx-xyz", result.ConversationId);
    }

    [Fact]
    public void ToDomain_WithNoContextId_ShouldReturnNullOrEmptyConversationId()
    {
        var proto = new ConversationResponseAlpha2();
        var result = proto.ToDomain();

        Assert.True(string.IsNullOrEmpty(result.ConversationId));
    }

    [Fact]
    public void ToDomain_WithEmptyOutputs_ShouldReturnEmptyOutputsList()
    {
        var proto = new ConversationResponseAlpha2 { ContextId = "ctx-1" };
        var result = proto.ToDomain();

        Assert.Empty(result.Outputs);
    }

    [Fact]
    public void ToDomain_ShouldMapOutputMessageContent()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Hello, how can I help?" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Single(result.Outputs);
        Assert.Single(result.Outputs[0].Choices);
        Assert.Equal("Hello, how can I help?", result.Outputs[0].Choices[0].Message.Content);
    }

    [Fact]
    public void ToDomain_WithStopFinishReason_ShouldMapToStopEnum()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Done." }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Equal(FinishReason.Stop, result.Outputs[0].Choices[0].FinishReason);
    }

    [Fact]
    public void ToDomain_WithLengthFinishReason_ShouldMapToLengthEnum()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "length",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Truncated..." }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Equal(FinishReason.Length, result.Outputs[0].Choices[0].FinishReason);
    }

    [Fact]
    public void ToDomain_WithToolCallsFinishReason_ShouldMapToToolCallsEnum()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "tool_calls",
            Index = 0,
            Message = new ConversationResultMessage { Content = "" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Equal(FinishReason.ToolCalls, result.Outputs[0].Choices[0].FinishReason);
    }

    [Fact]
    public void ToDomain_WithContentFilterFinishReason_ShouldMapToContentFilterEnum()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "content_filter",
            Index = 0,
            Message = new ConversationResultMessage { Content = "" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Equal(FinishReason.ContentFilter, result.Outputs[0].Choices[0].FinishReason);
    }

    [Fact]
    public void ToDomain_WithUnrecognizedFinishReason_ShouldReturnNullFinishReason()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "completely_unrecognized_xyz",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Hello" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Null(result.Outputs[0].Choices[0].FinishReason);
    }

    [Fact]
    public void ToDomain_ShouldMapChoiceIndex()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 3,
            Message = new ConversationResultMessage { Content = "Hello" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Equal(3L, result.Outputs[0].Choices[0].Index);
    }

    [Fact]
    public void ToDomain_ShouldMapModel()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2 { Model = "gpt-4o" };
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Hello" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Equal("gpt-4o", result.Outputs[0].Model);
    }

    [Fact]
    public void ToDomain_WithWhitespaceModelName_ShouldSetModelToNull()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2 { Model = "   " };
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Hello" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Null(result.Outputs[0].Model);
    }

    [Fact]
    public void ToDomain_WithToolCalls_ShouldMapToolCallDetails()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "tool_calls",
            Index = 0,
            Message = new ConversationResultMessage { Content = "" }
        };
        protoChoice.Message.ToolCalls.Add(new ConversationToolCalls
        {
            Id = "call-abc",
            Function = new ConversationToolCallsOfFunction
            {
                Name = "get_weather",
                Arguments = "{\"city\":\"Seattle\"}"
            }
        });
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        var toolCalls = result.Outputs[0].Choices[0].Message.ToolCalls;
        Assert.Single(toolCalls);
        var calledFunc = Assert.IsType<CalledToolFunction>(toolCalls[0]);
        Assert.Equal("call-abc", calledFunc.Id);
        Assert.Equal("get_weather", calledFunc.Name);
        Assert.Equal("{\"city\":\"Seattle\"}", calledFunc.JsonArguments);
    }

    [Fact]
    public void ToDomain_WithMultipleToolCalls_ShouldMapAll()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "tool_calls",
            Index = 0,
            Message = new ConversationResultMessage { Content = "" }
        };
        protoChoice.Message.ToolCalls.Add(new ConversationToolCalls
        {
            Id = "call-1",
            Function = new ConversationToolCallsOfFunction { Name = "func_a", Arguments = "{}" }
        });
        protoChoice.Message.ToolCalls.Add(new ConversationToolCalls
        {
            Id = "call-2",
            Function = new ConversationToolCallsOfFunction { Name = "func_b", Arguments = "{}" }
        });
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Equal(2, result.Outputs[0].Choices[0].Message.ToolCalls.Count);
    }

    [Fact]
    public void ToDomain_WithNoToolCalls_ShouldReturnEmptyToolCallsList()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "No tools used." }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Empty(result.Outputs[0].Choices[0].Message.ToolCalls);
    }

    [Fact]
    public void ToDomain_WithUsage_ShouldMapUsageTokens()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2
        {
            Usage = new ConversationResultAlpha2CompletionUsage
            {
                CompletionTokens = 120,
                PromptTokens = 80,
                TotalTokens = 200
            }
        };
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Response" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.NotNull(result.Outputs[0].Usage);
        Assert.Equal(120UL, result.Outputs[0].Usage!.CompletionTokens);
        Assert.Equal(80UL, result.Outputs[0].Usage!.PromptTokens);
        Assert.Equal(200UL, result.Outputs[0].Usage!.TotalTokens);
    }

    [Fact]
    public void ToDomain_WithUsageCompletionTokensDetails_ShouldMapDetails()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2
        {
            Usage = new ConversationResultAlpha2CompletionUsage
            {
                CompletionTokens = 50,
                PromptTokens = 30,
                TotalTokens = 80,
                CompletionTokensDetails = new ConversationResultAlpha2CompletionUsageCompletionTokensDetails
                {
                    AcceptedPredictionTokens = 10,
                    AudioTokens = 5,
                    ReasoningTokens = 20,
                    RejectedPredictionTokens = 15
                }
            }
        };
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Done" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        var details = result.Outputs[0].Usage!.CompletionTokensDetails;
        Assert.NotNull(details);
        Assert.Equal(10UL, details!.AcceptedPredictionTokens);
        Assert.Equal(5UL, details.AudioTokens);
        Assert.Equal(20UL, details.ReasoningTokens);
        Assert.Equal(15UL, details.RejectedPredictionTokens);
    }

    [Fact]
    public void ToDomain_WithUsagePromptTokensDetails_ShouldMapDetails()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2
        {
            Usage = new ConversationResultAlpha2CompletionUsage
            {
                CompletionTokens = 50,
                PromptTokens = 30,
                TotalTokens = 80,
                PromptTokensDetails = new ConversationResultAlpha2CompletionUsagePromptTokensDetails
                {
                    AudioTokens = 8,
                    CachedTokens = 22
                }
            }
        };
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Done" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        var details = result.Outputs[0].Usage!.PromptTokensDetails;
        Assert.NotNull(details);
        Assert.Equal(8UL, details!.AudioTokens);
        Assert.Equal(22UL, details.CachedTokens);
    }

    [Fact]
    public void ToDomain_WithNoUsage_ShouldReturnNullUsage()
    {
        var proto = new ConversationResponseAlpha2();
        var protoResult = new ConversationResultAlpha2();
        var protoChoice = new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Hello" }
        };
        protoResult.Choices.Add(protoChoice);
        proto.Outputs.Add(protoResult);

        var result = proto.ToDomain();

        Assert.Null(result.Outputs[0].Usage);
    }

    [Fact]
    public void ToDomain_WithMultipleOutputs_ShouldMapAll()
    {
        var proto = new ConversationResponseAlpha2();

        var protoResult1 = new ConversationResultAlpha2 { Model = "model-a" };
        protoResult1.Choices.Add(new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "First output" }
        });

        var protoResult2 = new ConversationResultAlpha2 { Model = "model-b" };
        protoResult2.Choices.Add(new ConversationResultChoices
        {
            FinishReason = "stop",
            Index = 0,
            Message = new ConversationResultMessage { Content = "Second output" }
        });

        proto.Outputs.Add(protoResult1);
        proto.Outputs.Add(protoResult2);

        var result = proto.ToDomain();

        Assert.Equal(2, result.Outputs.Count);
        Assert.Equal("model-a", result.Outputs[0].Model);
        Assert.Equal("model-b", result.Outputs[1].Model);
        Assert.Equal("First output", result.Outputs[0].Choices[0].Message.Content);
        Assert.Equal("Second output", result.Outputs[1].Choices[0].Message.Content);
    }

    #endregion

    private sealed class UnsupportedTool : ITool;
}
