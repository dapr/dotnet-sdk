﻿// ------------------------------------------------------------------------
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

namespace Dapr.AI.Conversation.Models.Request;

/// <summary>
/// Represents an input for the Dapr Conversational API.
/// </summary>
/// <param name="Message">The message to send to the LLM.</param>
/// <param name="ScrubPII">If true, scrubs the data that goes into the LLM.</param>
/// <param name="Role">The role indicating the entity providing the message.</param>
public sealed record DaprConversationInput(string Message, bool ScrubPII = false, DaprConversationRole? Role = null);