using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Dapr.Common.JsonConverters;

namespace Dapr.AI.Conversation;

/// <summary>
/// Represents who 
/// </summary>
public enum DaprConversationRole
{
    /// <summary>
    /// Represents a message sent by an AI.
    /// </summary>
    [EnumMember(Value="ai")]
    AI,
    /// <summary>
    /// Represents a message sent by a human.
    /// </summary>
    [EnumMember(Value="human")]
    Human,
    /// <summary>
    /// Represents a message sent by the system.
    /// </summary>
    [EnumMember(Value="system")]
    System,
    /// <summary>
    /// Represents a message sent by a generic user.
    /// </summary>
    [EnumMember(Value="generic")]
    Generic,
    /// <summary>
    /// Represents a message sent by a function.
    /// </summary>
    [EnumMember(Value="function")]
    Function,
    /// <summary>
    /// Represents a message sent by a tool.
    /// </summary>
    [EnumMember(Value="tool")]
    Tool
}
