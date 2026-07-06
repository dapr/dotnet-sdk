namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Options for <see cref="NumericVersionStrategy"/>.
/// </summary>
public sealed class NumericVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets the prefix used before the numeric suffix, for example <c>"V"</c> in <c>CartStateV1</c>.
    /// </summary>
    public string SuffixPrefix { get; set; } = "V";

    /// <summary>
    /// Gets or sets a value indicating whether prefix matching ignores case.
    /// </summary>
    public bool IgnorePrefixCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether names without a numeric suffix are allowed.
    /// </summary>
    public bool AllowNoSuffix { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether numeric suffixes must use zero-padding.
    /// </summary>
    public bool ZeroPad { get; set; }

    /// <summary>
    /// Gets or sets the required width for the numeric suffix when zero-padding is enabled.
    /// </summary>
    public int Width { get; set; } = 4;

    /// <summary>
    /// Gets or sets the default version used when no suffix is present.
    /// </summary>
    public string DefaultVersion { get; set; } = "0";
}
