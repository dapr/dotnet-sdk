using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Applies the generate, verify, deploy guard for interpreted machine definitions.
/// </summary>
public sealed class InterpretedMachineDeployer(IInterpretedMachineVerifier verifier, IInterpretedMachineStore store)
{
    /// <summary>
    /// Verifies and stores a machine definition for an interpreted actor instance.
    /// </summary>
    public async ValueTask DeployAsync(
        string actorType,
        ActorId actorId,
        InterpretedMachineDefinition definition,
        CancellationToken cancellationToken = default)
    {
        verifier.Verify(definition).ThrowIfInvalid();
        await store.SetAsync(actorType, actorId, definition, cancellationToken).ConfigureAwait(false);
    }
}
