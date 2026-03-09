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
/// Strongly-typed representation of a tool choice that supports known values ('none', 'auto') as well
/// as arbitrary tool names.
/// </summary>
public readonly record struct ToolChoice
{
    private readonly string _value;
    private const string NoneValue = "none";
    private const string AutoValue = "auto";
    private const string RequiredValue = "required";

    /// <summary>
    /// The current value of the <see cref="ToolChoice"/> instance.
    /// </summary>
    public string Value => _value;

    /// <summary>
    /// Initializes the <see cref="ToolChoice"/> instance with the value "none".
    /// </summary>
    public static ToolChoice None => new(NoneValue);
    /// <summary>
    /// Initializes the <see cref="ToolChoice"/> instance with the value "auto".
    /// </summary>
    public static ToolChoice Auto => new(AutoValue);
    /// <summary>
    /// Initializes the <see cref="ToolChoice"/> instance with the value "required".
    /// </summary>
    public static ToolChoice Required => new(RequiredValue);

    /// <summary>
    /// Validates whether the current <see cref="ToolChoice"/> instance is "none".
    /// </summary>
    public bool IsNone => string.Equals(_value, NoneValue, StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// Validates whether the current <see cref="ToolChoice"/> instance is "auto".
    /// </summary>
    public bool IsAuto => string.Equals(_value, AutoValue, StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// Validates whether the current <see cref="ToolChoice"/> instance is "required".
    /// </summary>
    public bool IsRequired => string.Equals(_value, RequiredValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Used to initialize a new <see cref="ToolChoice"/> instance with a known value.
    /// </summary>
    /// <param name="toolName">The case-sensitive tool name to call.</param>
    public ToolChoice(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace("Tool choice cannot be null or whitespace.", nameof(toolName));

        _value = toolName;
    }

    /// <summary>
    /// Overrides the default ToString() method to return the current value of the <see cref="ToolChoice"/> instance.
    /// </summary>
    /// <returns>A string containing the current tool choice value.</returns>
    public override string ToString() => _value;
}
