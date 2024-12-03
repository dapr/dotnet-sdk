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

using Google.Protobuf.WellKnownTypes;

namespace Dapr.AI.Conversation;

/// <summary>
/// The result for a single conversational input.
/// </summary>
/// <param name="Result">The result for one conversation input.</param>
public record DaprConversationResult(string Result)
{
    /// <summary>
    /// Parameters for all custom fields.
    /// </summary>
    public IReadOnlyDictionary<string, Any> Parameters { get; init; } = new Dictionary<string, Any>();
}
