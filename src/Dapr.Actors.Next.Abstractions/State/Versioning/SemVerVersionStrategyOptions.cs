namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Options for <see cref="SemVerVersionStrategy"/>.
/// </summary>
public sealed class SemVerVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets the prefix expected before the SemVer suffix, for example <c>"v"</c> in <c>CartStatev1.2.3</c>.
    /// </summary>
    public string Prefix { get; set; } = "v";

    /// <summary>
    /// Gets or sets a value indicating whether prefix matching ignores case.
    /// </summary>
    public bool IgnorePrefixCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pre-release labels are allowed.
    /// </summary>
    public bool AllowPrerelease { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether build metadata is allowed.
    /// </summary>
    public bool AllowBuildMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether names without a SemVer suffix are allowed.
    /// </summary>
    public bool AllowNoSuffix { get; set; }

    /// <summary>
    /// Gets or sets the default version used when no suffix is present.
    /// </summary>
    public string DefaultVersion { get; set; } = "0.0.0";
}
