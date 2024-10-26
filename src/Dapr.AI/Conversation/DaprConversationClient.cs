using Dapr.AI.Conversation.Models.Request;
using Dapr.AI.Conversation.Models.Response;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.AI.Conversation;

/// <summary>
/// Used to interact with the Dapr conversation building block.
/// </summary>
public sealed class DaprConversationClient : DaprAIClient
{
    /// <summary>
    /// The DaprClient instance.
    /// </summary>
    private readonly P.Dapr.DaprClient daprClient;
    
    /// <summary>
    /// Used to initialize a new instance of a <see cref="DaprConversationClient"/>.
    /// </summary>
    /// <param name="daprClient">The Dapr client.</param>
    public DaprConversationClient(P.Dapr.DaprClient daprClient)
    {
        this.daprClient = daprClient;
    }

    /// <summary>
    /// Sends various inputs to the large language model via the Conversational building block on the Dapr sidecar.
    /// </summary>
    /// <param name="daprConversationComponentName">The name of the Dapr conversation component.</param>
    /// <param name="inputs">The input values to send.</param>
    /// <param name="options">Optional options used to configure the conversation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response(s) provided by the LLM provider.</returns>
    public async Task<DaprConversationResponse> ConverseAsync(string daprConversationComponentName, List<DaprLlmInput> inputs, ConversationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new P.ConversationAlpha1Request
        {
            Name = daprConversationComponentName
        };

        if (options is not null)
        {
            request.ContextID = options.ConversationId;
            request.ScrubPII = options.ScrubPII;

            foreach (var (key, value) in options.Metadata)
            {
                request.Metadata.Add(key, value);
            }

            foreach (var (key, value) in options.Parameters)
            {
                request.Parameters.Add(key, value);
            }
        }

        foreach (var input in inputs)
        {
            request.Inputs.Add(new P.ConversationInput
            {
                ScrubPII = input.ScrubPII,
                Message = input.Message,
                Role = input.Role
            });
        }

        var result = await daprClient.ConverseAlpha1Async(request, cancellationToken: cancellationToken);
        var outputs = result.Outputs.Select(output => new DaprConversationResult(output.Result)
        {
            Parameters = output.Parameters.ToDictionary(kvp => kvp.Key, parameter => parameter.Value)
        }).ToList();

        return new DaprConversationResponse(outputs);
    }
}
