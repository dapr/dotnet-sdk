namespace Dapr.Actors.Next.Abstractions.Registry;

/// <summary>
/// Describes one actor method parameter emitted by the source generator.
/// </summary>
public sealed record ActorParameterDescriptor(
    string Name,
    Type ParameterType,
    int Position,
    bool IsCancellationToken,
    bool HasDefaultValue,
    object? DefaultValue);
