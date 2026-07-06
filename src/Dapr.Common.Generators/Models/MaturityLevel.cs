namespace Dapr.Common.Generators.Models;

/// <summary>
/// Maturity level of a gRPC method variant.
/// </summary>
internal enum MaturityLevel
{
    Alpha = 1,
    Beta = 2,
    ReleaseCandidate = 3,
    Stable = 4
}
