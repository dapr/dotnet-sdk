namespace Dapr.Actors.Next.Abstractions.Filters;

/// <summary>
/// Context passed to named interpreted actor capabilities.
/// </summary>
public sealed record ActorCapabilityContext(
    string ActorType,
    ActorId ActorId,
    string CapabilityName,
    IReadOnlyDictionary<string, object?> Arguments,
    ActorRequestContext RequestContext);
