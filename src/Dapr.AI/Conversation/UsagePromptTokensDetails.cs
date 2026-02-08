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
/// Represents the breakdown of prompt tokens used in a conversation.
/// </summary>
/// <param name="AudioTokens">Audio input tokens present in the prompt.</param>
/// <param name="CachedTokens">Cached tokens present in the prompt.</param>
public sealed record UsagePromptTokensDetails(ulong AudioTokens, ulong CachedTokens)
{
    /// <summary>
    /// Creates an instance of <see cref="UsagePromptTokensDetails"/> from the prototype value.
    /// </summary>
    /// <param name="proto">The gRPC response from the runtime to parse.</param>
    /// <returns>A new instance of <see cref="UsagePromptTokensDetails"/>.</returns>
    internal static UsagePromptTokensDetails
        FromProto(ConversationResultAlpha2CompletionUsagePromptTokensDetails proto) =>
        new(proto.AudioTokens, proto.CachedTokens);
}
