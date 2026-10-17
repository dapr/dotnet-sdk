using Xunit;

namespace Dapr.Testcontainers.Xunit.Attributes;

/// <summary>
/// Used to indicate that there's a minimum supported version of the Dapr runtime needed to run the indicated
/// integration test and that it should be skipped otherwise.
/// </summary>
/// <remarks>
/// This will include RCs of that indicated version in addition to stable versions.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MinimumDaprRuntimeFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes the <see cref="MinimumDaprRuntimeFactAttribute"/> instance.
    /// </summary>
    /// <param name="minimumVersion">The minimum supported version.</param>
    public MinimumDaprRuntimeFactAttribute(string minimumVersion)
    {
        if (!DaprRuntimeVersionGate.IsMinimumSatisfied(minimumVersion, out var reason))
            Skip = reason;
    }
}
