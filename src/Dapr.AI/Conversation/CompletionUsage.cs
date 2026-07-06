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

using Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.AI.Conversation;

/// <summary>
/// The breakdown of tokens used in a conversation.
/// </summary>
/// <param name="CompletionTokens">Number of tokens in the generated completion.</param>
/// <param name="PromptTokens">The number of tokes in the prompt.</param>
/// <param name="TotalTokens">The total number of tokens used in the request (prompt and completion).</param>
public sealed record CompletionUsage(ulong CompletionTokens, ulong PromptTokens, ulong TotalTokens)
{
    /// <summary>
    /// Breakdown of tokens used in completion.
    /// </summary>
    public UsageCompletionTokensDetails? CompletionTokensDetails { get; init; }

    /// <summary>
    /// Breakdown of tokens used in the prompt.
    /// </summary>
    public UsagePromptTokensDetails? PromptTokensDetails { get; init; }

    /// <summary>
    /// Creates an instance of <see cref="CompletionUsage"/> from the prototype value.
    /// </summary>
    /// <param name="proto">The gRPC response from the runtime to parse.</param>
    /// <returns>A new instance of <see cref="CompletionUsage"/>.</returns>
    internal static CompletionUsage? FromProto(ConversationResultAlpha2CompletionUsage? proto) =>
        proto is null
            ? null
            : new(proto.CompletionTokens, proto.PromptTokens, proto.TotalTokens)
            {
                CompletionTokensDetails = UsageCompletionTokensDetails.FromProto(proto.CompletionTokensDetails),
                PromptTokensDetails = UsagePromptTokensDetails.FromProto(proto.PromptTokensDetails)
            };
}


