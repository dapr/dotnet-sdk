namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Structural analysis result for a compiled state machine.
/// </summary>
public sealed record StateMachineStructuralAnalysis(Type ActorType, IReadOnlyList<string> StructuralDefects);
