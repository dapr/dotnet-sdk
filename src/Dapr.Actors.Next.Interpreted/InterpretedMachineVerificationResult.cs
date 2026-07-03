namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Structural and behavioral verification result for an interpreted machine definition.
/// </summary>
public sealed record InterpretedMachineVerificationResult(IReadOnlyList<string> Defects)
{
    /// <summary>
    /// Gets a value indicating whether no defects were found.
    /// </summary>
    public bool IsValid => Defects.Count == 0;

    /// <summary>
    /// Throws when the definition has verification defects.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("Interpreted machine verification failed: " + string.Join("; ", Defects));
        }
    }
}
