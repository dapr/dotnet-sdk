namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Result returned by <see cref="ActorStateMachine.Analyze{TActor}"/>.
/// </summary>
public sealed record ActorStateMachineAnalysis(
    Type ActorType,
    IReadOnlyList<ActorReachableMethod> ReachableMethods,
    IReadOnlyList<string> StructuralDefects)
{
    /// <summary>
    /// Throws if structural defects were found.
    /// </summary>
    public void AssertNoStructuralDefects()
    {
        if (StructuralDefects.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, StructuralDefects));
        }
    }
}
