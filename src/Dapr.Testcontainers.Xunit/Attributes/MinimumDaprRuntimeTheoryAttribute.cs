using Xunit;

namespace Dapr.Testcontainers.Xunit.Attributes;

/// <summary>
/// Used to indicate that there's a minimum supported version of the Dapr runtime needed to run the indicated
/// integration theory and that it should be skipped otherwise.
/// </summary>
/// <remarks>
/// This will include RCs of that indicated version in addition to stable versions.
/// The rolling runtime label <c>edge</c> (case-insensitive) satisfies every valid numerical minimum.
/// The runtime version is read from DAPR_RUNTIME_VERSION, using the same rules as
/// <see cref="MinimumDaprRuntimeFactAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MinimumDaprRuntimeTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes the <see cref="MinimumDaprRuntimeTheoryAttribute"/> instance.
    /// </summary>
    /// <param name="minimumVersion">The minimum supported version.</param>
    public MinimumDaprRuntimeTheoryAttribute(string minimumVersion)
    {
        if (!DaprRuntimeVersionGate.IsMinimumSatisfied(minimumVersion, out var reason))
            Skip = reason;
    }
}
