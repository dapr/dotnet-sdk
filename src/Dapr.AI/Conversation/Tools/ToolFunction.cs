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

namespace Dapr.AI.Conversation.Tools;

/// <summary>
/// The main tool type to be used in a conversation.
/// </summary>
/// <param name="Name">The name of the function to be called.</param>
public record ToolFunction(string Name) : ITool
{
    /// <summary>
    /// A description of what the function does. The model uses this to choose when and how to
    /// call the function.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// The parameters the function accepts.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}
