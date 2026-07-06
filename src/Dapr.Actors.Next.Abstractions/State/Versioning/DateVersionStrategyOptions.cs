namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Options for <see cref="DateVersionStrategy"/>.
/// </summary>
public sealed class DateVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets the date format expected at the end of the actor state type name.
    /// </summary>
    public string DateFormat { get; set; } = "yyyyMMdd";

    /// <summary>
    /// Gets or sets the prefix expected before the date suffix.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether prefix matching ignores case.
    /// </summary>
    public bool IgnorePrefixCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether names without a date suffix are allowed.
    /// </summary>
    public bool AllowNoSuffix { get; set; }

    /// <summary>
    /// Gets or sets the default version used when no suffix is present.
    /// </summary>
    public string DefaultVersion { get; set; } = "0";
}
