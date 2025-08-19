using System.Runtime.Serialization;

namespace Dapr.AI.Conversation.Tools;

/// <summary>
/// The reason the model stopped generating tokens.
/// </summary>
public enum FinishReason
{
    /// <summary>
    /// Indicates that the model has hit a natural stop point or a prodivded stop sequence.
    /// </summary>
    [EnumMember(Value="stop")]
    Stop,
    /// <summary>
    /// Indicates that the maximum number of tokens specified in the request has been reached.
    /// </summary>
    [EnumMember(Value="length")]
    Length,
    /// <summary>
    /// Indicates that the model called a tool.
    /// </summary>
    [EnumMember(Value="tool_calls")]
    ToolCalls,
    /// <summary>
    /// Indicates that the content was omitted due to a flag from a content filter.
    /// </summary>
    [EnumMember(Value="content_filter")]
    ContentFilter,
    /// <summary>
    /// Represents any other unspecified or unrecognized reason.
    /// </summary>
    [EnumMember(Value="unknown")]
    Unknown
}
