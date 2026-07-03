namespace Dapr.Actors.Next.Abstractions.Registry;

/// <summary>
/// Describes one actor method emitted by the source generator.
/// </summary>
public sealed record ActorMethodDescriptor(
    string Name,
    string WireName,
    Type ReturnType,
    IReadOnlyList<ActorParameterDescriptor> Parameters);
