namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Verifies interpreted machine definitions before deployment or activation.
/// </summary>
public interface IInterpretedMachineVerifier
{
    /// <summary>
    /// Verifies a machine definition against structural and behavioral checks.
    /// </summary>
    InterpretedMachineVerificationResult Verify(InterpretedMachineDefinition definition);
}
