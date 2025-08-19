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
    /// Validates whether the current <see cref="ToolChoice"/> instance is "none".
    /// </summary>
    public bool IsNone => string.Equals(_value, NoneValue, StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// Validates whether the current <see cref="ToolChoice"/> instance is "auto".
    /// </summary>
    public bool IsAuto => string.Equals(_value, AutoValue, StringComparison.OrdinalIgnoreCase);

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
