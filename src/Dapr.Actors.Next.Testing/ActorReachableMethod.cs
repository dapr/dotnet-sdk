namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Describes a public actor method reachable by the structural-analysis placeholder.
/// </summary>
public sealed record ActorReachableMethod(string Name, Type ReturnType, IReadOnlyList<Type> ParameterTypes);
