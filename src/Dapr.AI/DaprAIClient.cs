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

using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Models.Request;
using Dapr.AI.Conversation.Models.Response;

namespace Dapr.AI;

/// <summary>
/// The base implementation of a Dapr AI client.
/// </summary>
public abstract class DaprAIClient
{
    /// <summary>
    /// Sends various inputs to the large language model via the Conversational building block on the Dapr sidecar.
    /// </summary>
    /// <param name="daprConversationComponentName">The name of the Dapr conversation component.</param>
    /// <param name="inputs">The input values to send.</param>
    /// <param name="options">Optional options used to configure the conversation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response(s) provided by the LLM provider.</returns>
    public abstract Task<DaprConversationResponse> ConverseAsync(string daprConversationComponentName,
        IReadOnlyList<DaprConversationInput> inputs, ConversationOptions? options = null,
        CancellationToken cancellationToken = default);
}
