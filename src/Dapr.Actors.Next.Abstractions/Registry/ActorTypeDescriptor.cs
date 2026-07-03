namespace Dapr.Actors.Next.Abstractions.Registry;

/// <summary>
/// Describes an actor type emitted by the source generator.
/// </summary>
public sealed record ActorTypeDescriptor(
    string ActorType,
    int ContractVersion,
    Type ImplementationType,
    Type InterfaceType,
    IReadOnlyList<ActorMethodDescriptor> Methods);
