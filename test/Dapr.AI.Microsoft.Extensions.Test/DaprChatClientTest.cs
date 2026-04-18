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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Tools;
using Dapr.AI.Microsoft.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.AI.Microsoft.Extensions.Test;

public class DaprChatClientTest
{
    private static IOptions<DaprChatClientOptions> CreateOptions(string componentName = "test-component") =>
        Options.Create(new DaprChatClientOptions { ConversationComponentName = componentName });

    private static ConversationResponse BuildResponse(
        string text,
        FinishReason? finishReason = null,
        string? conversationId = null)
    {
        var message = new ResultMessage(text);
        var choice = new ConversationResultChoice(finishReason, 0, message);
        var output = new ConversationResponseResult(new ConversationResultChoice[] { choice });
        return new ConversationResponse(new ConversationResponseResult[] { output }, conversationId);
    }

    #region Constructor

    [Fact]
    public void Constructor_NullOptions_ShouldThrowArgumentNullException()
    {
        var fake = new FakeConversationClient(new ConversationResponse(new ConversationResponseResult[0]));
        var sp = new ServiceCollection().BuildServiceProvider();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new DaprChatClient(fake, sp, null!));
        Assert.Equal("daprClientOptions", ex.ParamName);
    }

    #endregion

    #region GetResponseAsync - Message role mapping

    [Fact]
    public async Task GetResponseAsync_UserMessage_MapsToUserMessage()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "Hello") { AuthorName = "alice" } },
            cancellationToken: TestContext.Current.CancellationToken);

        var msgs = fake.CapturedInputs![0].Messages;
        Assert.Single(msgs);
        var msg = Assert.IsType<UserMessage>(msgs[0]);
        Assert.Equal("alice", msg.Name);
        Assert.Equal("Hello", msg.Content[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_AssistantMessage_MapsToAssistantMessage()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.Assistant, "I can help.") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<AssistantMessage>(fake.CapturedInputs![0].Messages[0]);
    }

    [Fact]
    public async Task GetResponseAsync_SystemMessage_MapsToSystemMessage()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.System, "Be helpful.") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<SystemMessage>(fake.CapturedInputs![0].Messages[0]);
    }

    [Fact]
    public async Task GetResponseAsync_ToolMessage_MapsToToolMessage()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.Tool, "sunny") { AuthorName = "weather_api" } },
            cancellationToken: TestContext.Current.CancellationToken);

        var toolMsg = Assert.IsType<ToolMessage>(fake.CapturedInputs![0].Messages[0]);
        Assert.Equal("weather_api", toolMsg.Name);
        Assert.Equal("sunny", toolMsg.Content[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_ToolMessageWithNullAuthorName_UsesEmptyStringAsName()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.Tool, "result") },
            cancellationToken: TestContext.Current.CancellationToken);

        var toolMsg = Assert.IsType<ToolMessage>(fake.CapturedInputs![0].Messages[0]);
        Assert.Equal(string.Empty, toolMsg.Name);
    }

    [Fact]
    public async Task GetResponseAsync_UnknownRole_ThrowsArgumentOutOfRangeException()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GetResponseAsync(
                new[] { new ChatMessage(new ChatRole("custom_role"), "text") },
                cancellationToken: TestContext.Current.CancellationToken));
    }

    #endregion

    #region GetResponseAsync - Options mapping

    [Fact]
    public async Task GetResponseAsync_NullChatOptions_UsesComponentNameFromDaprOptions()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions("my-llm"));

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("my-llm", fake.CapturedOptions!.ConversationComponentId);
    }

    [Fact]
    public async Task GetResponseAsync_WithTemperature_MapsTemperature()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { Temperature = 0.7f },
            TestContext.Current.CancellationToken);

        Assert.Equal(0.7f, fake.CapturedOptions!.Temperature);
    }

    [Fact]
    public async Task GetResponseAsync_WithConversationId_MapsToContextId()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { ConversationId = "ctx-42" },
            TestContext.Current.CancellationToken);

        Assert.Equal("ctx-42", fake.CapturedOptions!.ContextId);
    }

    [Fact]
    public async Task GetResponseAsync_WithAdditionalProperties_MapsToMetadata()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var options = new ChatOptions();
        options.AdditionalProperties = new AdditionalPropertiesDictionary { { "max_tokens", 100 } };
        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options,
            TestContext.Current.CancellationToken);

        Assert.True(fake.CapturedOptions!.Metadata.ContainsKey("max_tokens"));
        Assert.Equal("100", fake.CapturedOptions!.Metadata["max_tokens"]);
    }

    [Fact]
    public async Task GetResponseAsync_WithNullAdditionalProperties_HasEmptyMetadata()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { AdditionalProperties = null },
            TestContext.Current.CancellationToken);

        Assert.Empty(fake.CapturedOptions!.Metadata);
    }

    [Fact]
    public async Task GetResponseAsync_WithAutoToolMode_MapsToAutoToolChoice()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { ToolMode = ChatToolMode.Auto },
            TestContext.Current.CancellationToken);

        Assert.NotNull(fake.CapturedOptions!.ToolChoice);
        Assert.True(fake.CapturedOptions!.ToolChoice!.Value.IsAuto);
    }

    [Fact]
    public async Task GetResponseAsync_WithNoneToolMode_MapsToNoneToolChoice()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { ToolMode = ChatToolMode.None },
            TestContext.Current.CancellationToken);

        Assert.NotNull(fake.CapturedOptions!.ToolChoice);
        Assert.True(fake.CapturedOptions!.ToolChoice!.Value.IsNone);
    }

    [Fact]
    public async Task GetResponseAsync_WithRequireAnyToolMode_MapsToRequiredToolChoice()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { ToolMode = ChatToolMode.RequireAny },
            TestContext.Current.CancellationToken);

        Assert.NotNull(fake.CapturedOptions!.ToolChoice);
        Assert.True(fake.CapturedOptions!.ToolChoice!.Value.IsRequired);
    }

    [Fact]
    public async Task GetResponseAsync_WithRequireSpecificToolMode_MapsToNamedToolChoice()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { ToolMode = ChatToolMode.RequireSpecific("get_weather") },
            TestContext.Current.CancellationToken);

        Assert.NotNull(fake.CapturedOptions!.ToolChoice);
        Assert.Equal("get_weather", fake.CapturedOptions!.ToolChoice!.Value.Value);
    }

    [Fact]
    public async Task GetResponseAsync_WithNullToolMode_LeavesToolChoiceNull()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { ToolMode = null },
            TestContext.Current.CancellationToken);

        Assert.Null(fake.CapturedOptions!.ToolChoice);
    }

    [Fact]
    public async Task GetResponseAsync_WithTools_MapsToToolFunctions()
    {
        var fake = new FakeConversationClient(BuildResponse("reply"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var tool = AIFunctionFactory.Create(() => "sunny", new AIFunctionFactoryOptions
        {
            Name = "get_weather",
            Description = "Gets the weather"
        });
        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            new ChatOptions { Tools = new AITool[] { tool } },
            TestContext.Current.CancellationToken);

        Assert.Single(fake.CapturedOptions!.Tools);
        var toolFunc = Assert.IsType<ToolFunction>(fake.CapturedOptions!.Tools[0]);
        Assert.Equal("get_weather", toolFunc.Name);
        Assert.Equal("Gets the weather", toolFunc.Description);
    }

    #endregion

    #region GetResponseAsync - Response mapping

    [Fact]
    public async Task GetResponseAsync_ShouldReturnConversationId()
    {
        var fake = new FakeConversationClient(BuildResponse("reply", conversationId: "conv-123"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("conv-123", response.ConversationId);
    }

    [Fact]
    public async Task GetResponseAsync_WithTextContent_ReturnsTextContent()
    {
        var fake = new FakeConversationClient(BuildResponse("Hello!"));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(response.Messages);
        var textContent = Assert.IsType<TextContent>(response.Messages[0].Contents[0]);
        Assert.Equal("Hello!", textContent.Text);
    }

    [Fact]
    public async Task GetResponseAsync_WithEmptyTextContent_ReturnsMessageWithNoContents()
    {
        var message = new ResultMessage("");
        var choice = new ConversationResultChoice(null, 0, message);
        var daprResponse = new ConversationResponse(
            new ConversationResponseResult[]
            {
                new ConversationResponseResult(new ConversationResultChoice[] { choice })
            });

        var fake = new FakeConversationClient(daprResponse);
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var chatResponse = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(chatResponse.Messages);
        Assert.Empty(chatResponse.Messages[0].Contents);
    }

    [Fact]
    public async Task GetResponseAsync_WithEmptyOutputs_ReturnsEmptyMessages()
    {
        var fake = new FakeConversationClient(new ConversationResponse(new ConversationResponseResult[0]));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(response.Messages);
    }

    [Fact]
    public async Task GetResponseAsync_WithFinishReasonStop_ReturnsStop()
    {
        var fake = new FakeConversationClient(BuildResponse("reply", FinishReason.Stop));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
    }

    [Fact]
    public async Task GetResponseAsync_WithFinishReasonContentFilter_ReturnsContentFilter()
    {
        var fake = new FakeConversationClient(BuildResponse("reply", FinishReason.ContentFilter));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ChatFinishReason.ContentFilter, response.FinishReason);
    }

    [Fact]
    public async Task GetResponseAsync_WithFinishReasonLength_ReturnsLength()
    {
        var fake = new FakeConversationClient(BuildResponse("reply", FinishReason.Length));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ChatFinishReason.Length, response.FinishReason);
    }

    [Fact]
    public async Task GetResponseAsync_WithFinishReasonToolCalls_ReturnsToolCalls()
    {
        var fake = new FakeConversationClient(BuildResponse("reply", FinishReason.ToolCalls));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
    }

    [Fact]
    public async Task GetResponseAsync_WithNullFinishReason_ReturnsNullFinishReason()
    {
        var fake = new FakeConversationClient(BuildResponse("reply", null));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(response.FinishReason);
    }

    [Fact]
    public async Task GetResponseAsync_WithToolCall_ReturnsFunctionCallContent()
    {
        var toolCall = new CalledToolFunction("get_weather", "{\"city\":\"NYC\"}") { Id = "call-1" };
        var message = new ResultMessage("") { ToolCalls = new ToolCallBase[] { toolCall } };
        var choice = new ConversationResultChoice(FinishReason.ToolCalls, 0, message);
        var daprResponse = new ConversationResponse(
            new ConversationResponseResult[]
            {
                new ConversationResponseResult(new ConversationResultChoice[] { choice })
            });

        var fake = new FakeConversationClient(daprResponse);
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(response.Messages);
        var funcCall = Assert.IsType<FunctionCallContent>(response.Messages[0].Contents[0]);
        Assert.Equal("call-1", funcCall.CallId);
        Assert.Equal("get_weather", funcCall.Name);
        Assert.NotNull(funcCall.Arguments);
        Assert.True(funcCall.Arguments!.ContainsKey("city"));
    }

    [Fact]
    public async Task GetResponseAsync_WithInvalidJsonArguments_ReturnsEmptyArguments()
    {
        var toolCall = new CalledToolFunction("my_func", "not-valid-json") { Id = "call-2" };
        var message = new ResultMessage("") { ToolCalls = new ToolCallBase[] { toolCall } };
        var choice = new ConversationResultChoice(FinishReason.ToolCalls, 0, message);
        var daprResponse = new ConversationResponse(
            new ConversationResponseResult[]
            {
                new ConversationResponseResult(new ConversationResultChoice[] { choice })
            });

        var fake = new FakeConversationClient(daprResponse);
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(response.Messages);
        var funcCall = Assert.IsType<FunctionCallContent>(response.Messages[0].Contents[0]);
        Assert.Equal("call-2", funcCall.CallId);
        Assert.Equal("my_func", funcCall.Name);
        Assert.NotNull(funcCall.Arguments);
        Assert.Empty(funcCall.Arguments!);
    }

    [Fact]
    public async Task GetResponseAsync_WithToolCallNullId_UsesEmptyStringForCallId()
    {
        var toolCall = new CalledToolFunction("my_func", "{}");
        var message = new ResultMessage("") { ToolCalls = new ToolCallBase[] { toolCall } };
        var choice = new ConversationResultChoice(FinishReason.ToolCalls, 0, message);
        var daprResponse = new ConversationResponse(
            new ConversationResponseResult[]
            {
                new ConversationResponseResult(new ConversationResultChoice[] { choice })
            });

        var fake = new FakeConversationClient(daprResponse);
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        var funcCall = Assert.IsType<FunctionCallContent>(response.Messages[0].Contents[0]);
        Assert.Equal(string.Empty, funcCall.CallId);
    }

    [Fact]
    public async Task GetResponseAsync_WithMultipleChoices_OrdersByIndex()
    {
        var choice1 = new ConversationResultChoice(null, 1, new ResultMessage("second"));
        var choice0 = new ConversationResultChoice(null, 0, new ResultMessage("first"));
        var output = new ConversationResponseResult(new ConversationResultChoice[] { choice1, choice0 });
        var daprResponse = new ConversationResponse(new ConversationResponseResult[] { output });

        var fake = new FakeConversationClient(daprResponse);
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, response.Messages.Count);
        Assert.Equal("first", ((TextContent)response.Messages[0].Contents[0]).Text);
        Assert.Equal("second", ((TextContent)response.Messages[1].Contents[0]).Text);
    }

    #endregion

    #region GetStreamingResponseAsync

    [Fact]
    public void GetStreamingResponseAsync_ThrowsNotImplementedException()
    {
        var fake = new FakeConversationClient(new ConversationResponse(new ConversationResponseResult[0]));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());

        Assert.Throws<NotImplementedException>(() =>
            client.GetStreamingResponseAsync(
                new[] { new ChatMessage(ChatRole.User, "hi") },
                cancellationToken: TestContext.Current.CancellationToken));
    }

    #endregion

    #region GetService

    [Fact]
    public void GetService_WithoutKey_DelegatesToServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDisposable, DummyDisposable>();
        var sp = services.BuildServiceProvider();

        var fake = new FakeConversationClient(new ConversationResponse(new ConversationResponseResult[0]));
        var client = new DaprChatClient(fake, sp, CreateOptions());

        var result = client.GetService(typeof(IDisposable));
        Assert.IsType<DummyDisposable>(result);
    }

    [Fact]
    public void GetService_WithKey_DelegatesToKeyedServiceProvider()
    {
        // GetService(serviceType, serviceKey) calls GetKeyedService<object>(serviceKey),
        // so the service must be registered as the 'object' type.
        var dummy = new DummyDisposable();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<object>("my-key", dummy);
        var sp = services.BuildServiceProvider();

        var fake = new FakeConversationClient(new ConversationResponse(new ConversationResponseResult[0]));
        var client = new DaprChatClient(fake, sp, CreateOptions());

        var result = client.GetService(typeof(object), "my-key");
        Assert.Same(dummy, result);
    }

    [Fact]
    public void GetService_UnregisteredType_ReturnsNull()
    {
        var sp = new ServiceCollection().BuildServiceProvider();

        var fake = new FakeConversationClient(new ConversationResponse(new ConversationResponseResult[0]));
        var client = new DaprChatClient(fake, sp, CreateOptions());

        var result = client.GetService(typeof(IDisposable));
        Assert.Null(result);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var fake = new FakeConversationClient(new ConversationResponse(new ConversationResponseResult[0]));
        var client = new DaprChatClient(fake, new ServiceCollection().BuildServiceProvider(), CreateOptions());
        client.Dispose(); // Should not throw
    }

    #endregion

    private sealed class FakeConversationClient : DaprConversationClient
    {
        private readonly ConversationResponse _response;

        public FakeConversationClient(ConversationResponse response)
            : base(null!, new HttpClient())
        {
            _response = response;
        }

        public IReadOnlyList<ConversationInput>? CapturedInputs { get; private set; }
        public ConversationOptions? CapturedOptions { get; private set; }

        public override Task<ConversationResponse> ConverseAsync(
            IReadOnlyList<ConversationInput> inputs,
            ConversationOptions options,
            CancellationToken cancellationToken = default)
        {
            CapturedInputs = inputs;
            CapturedOptions = options;
            return Task.FromResult(_response);
        }
    }

    private sealed class DummyDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
