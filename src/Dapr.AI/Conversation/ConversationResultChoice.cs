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

using Dapr.AI.Conversation.Tools;

namespace Dapr.AI.Conversation;

/// <summary>
/// Represents a choice made by the model in the conversation.
/// </summary>
/// <param name="FinishReason">The reason why the model stopped generating tokens.</param>
/// <param name="Index">The index of the choice in the list of choices.</param>
/// <param name="Message">The message provided with the choice.</param>
public record ConversationResultChoice(FinishReason? FinishReason, long Index, ResultMessage Message);
