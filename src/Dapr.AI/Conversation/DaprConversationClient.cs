// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Dapr.AI.Conversation.Models.Request;
using Dapr.AI.Conversation.Models.Response;
using Dapr.Common.Extensions;
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
    public override async Task<DaprConversationResponse> ConverseAsync(string daprConversationComponentName, IReadOnlyList<DaprConversationInput> inputs, ConversationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new P.ConversationRequest
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
                Role = input.Role?.GetValueFromEnumMember() ?? null
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
