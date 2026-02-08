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

namespace Dapr.AI.Conversation;

/// <summary>
/// Documents a tool call by a function within the context of a conversation message.
/// </summary>
/// <param name="Name">The name of the tool called.</param>
/// <param name="JsonArguments">The JSON arguments populated by the model. These might be hallucinated and invalid (e.g. format, values, etc.).</param>
public record CalledToolFunction(string Name, string JsonArguments) : ToolCallBase;
