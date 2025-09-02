using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Type = System.Type;

namespace Dapr.AI.Microsoft.Extensions;

/// <summary>
/// Provides a concrete implementation of <see cref="IChatClient"/> that uses the Dapr Conversation component.
/// </summary>
[Experimental("DAPR_CONVERSATION", UrlFormat = "https://docs.dapr.io/developing-applications/building-blocks/conversation/conversation-overview/")]
public class DaprChatClient(DaprConversationClient daprClient, IServiceProvider serviceProvider, IOptions<DaprChatClientOptions> daprClientOptions) : IChatClient
{
    private readonly DaprChatClientOptions _options = daprClientOptions?.Value ?? throw new ArgumentNullException(nameof(daprClientOptions));

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var input = new ConversationInput(
            messages.Select(message =>
                {
                    if (message.Role == ChatRole.User)
                    {
                        return new UserMessage
                        {
                            Name = message.AuthorName,
                            Content =
                            [
                                new MessageContent(message.Text)
                            ]
                        };
                    }

                    if (message.Role == ChatRole.Assistant)
                    {
                        return new AssistantMessage
                        {
                            Name = message.AuthorName,
                            Content =
                            [
                                new MessageContent(message.Text)
                            ]
                        };
                    }

                    if (message.Role == ChatRole.System)
                    {
                        return new SystemMessage
                        {
                            Name = message.AuthorName,
                            Content =
                            [
                                new MessageContent(message.Text)
                            ]
                        };
                    }

                    if (message.Role == ChatRole.Tool)
                    {
                        return new ToolMessage
                        {
                            Name = message.AuthorName ?? string.Empty,
                            Content =
                            [
                                new MessageContent(message.Text),
                            ]
                        } as IConversationMessage;
                    }

                    throw new ArgumentOutOfRangeException($"Unknown message role: {message.Role.Value}",
                        nameof(message.Role));
                })
                .ToList());
        var conversationOptions = MapToOptions(this._options.ConversationComponentName, options);
        
        var response = await daprClient.ConverseAsync([input], conversationOptions, cancellationToken);
        return MapToResponse(response);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = new())
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is not null
        ? serviceProvider.GetKeyedService<object>(serviceKey)
        : serviceProvider.GetService(serviceType);

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to dispose.
    }

    /// <summary>
    /// Maps the provided <see cref="ConversationResponse"/> to a <see cref="ChatResponse"/> instance.
    /// </summary>
    /// <param name="response">The response from the Dapr runtime to map.</param>
    /// <returns>A populated instance of the <see cref="ChatResponse"/>.</returns>
    private static ChatResponse MapToResponse(ConversationResponse response) =>
        new()
        {
            ConversationId = response.ConversationId,
            FinishReason = MapFinishReason(response.Outputs.FirstOrDefault()?.Choices.FirstOrDefault()?.FinishReason),
            Messages = response.Outputs.SelectMany(output =>
                output.Choices
                    .OrderBy(a => a.Index)
                    .Select(choice =>
                    {
                        var content = new List<AIContent>();

                        // Add text content if available
                        if (!string.IsNullOrEmpty(choice.Message.Content))
                        {
                            content.Add(new TextContent(choice.Message.Content));
                        }

                        // Add tool calls as FunctionCallContent
                        foreach (var toolCall in choice.Message.ToolCalls)
                        {
                            if (toolCall is CalledToolFunction calledToolFunction)
                            {
                                content.Add(new FunctionCallContent(calledToolFunction.Id ?? string.Empty,
                                    calledToolFunction.Name,
                                    JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                        calledToolFunction.JsonArguments)));
                            }
                        }

                        return new ChatMessage { Contents = content, RawRepresentation = choice };
                    })).ToList()
        };

    /// <summary>
    /// Maps the provided <see cref="FinishReason"/> to a <see cref="ChatFinishReason"/> instance.
    /// </summary>
    /// <param name="finishReason">The resulting FinishReason to map.</param>
    /// <returns>The mapped <see cref="ChatFinishReason"/>.</returns>
    private static ChatFinishReason? MapFinishReason(FinishReason? finishReason) =>
        finishReason switch
        {
            FinishReason.Stop => ChatFinishReason.Stop,
            FinishReason.ContentFilter => ChatFinishReason.ContentFilter,
            FinishReason.Length => ChatFinishReason.Length,
            FinishReason.ToolCalls => ChatFinishReason.ToolCalls,
            _ => null
        };

    /// <summary>
    /// Maps the provided <see cref="ChatOptions"/> to a <see cref="ConversationOptions"/> instance.
    /// </summary>
    /// <param name="conversationComponentId">The name of the Dapr Conversation component.</param>
    /// <param name="options">The <see cref="ChatOptions"/> provided with the call.</param>
    /// <returns>A populated instance of <see cref="ConversationOptions"/>.</returns>
    private static ConversationOptions MapToOptions(string conversationComponentId, ChatOptions? options)
    {
        ToolChoice? toolChoice = null;
        if (options?.ToolMode is not null)
        {
            toolChoice = options.ToolMode switch
            {
                AutoChatToolMode => ToolChoice.Auto,
                NoneChatToolMode => ToolChoice.None,
                RequiredChatToolMode requiredMode => requiredMode.RequiredFunctionName == null
                    ? ToolChoice.Required
                    : new ToolChoice(requiredMode.RequiredFunctionName),
                _ => toolChoice
            };
        }
        
        var parameters = new Dictionary<string, string>();
        if (options?.AdditionalProperties is not null)
        {
            var clonedProperties = options.AdditionalProperties.Clone();
            foreach (var property in clonedProperties)
            {
                parameters[property.Key] = property.Value?.ToString() ?? string.Empty;
            }
        }

        var tools = new List<ITool>();
        if (options?.Tools is not null)
        {
            tools.AddRange(options.Tools.Select(tool =>
                new ToolFunction(tool.Name)
                {
                    Description = tool.Description, Parameters = tool.AdditionalProperties
                }));
        }

        return new ConversationOptions(conversationComponentId)
        {
            Temperature = options?.Temperature, 
            ToolChoice = toolChoice,
            ContextId = options?.ConversationId,
            Metadata = parameters,
            Tools = tools
        };
    }
}
