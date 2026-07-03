using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Stores actor types explicitly requested by the app.
/// </summary>
public sealed class DaprActorRegistrationCollection
{
    private readonly Dictionary<Type, DaprActorRegistration> registrations = new();

    /// <summary>
    /// Registers an actor type for hosting.
    /// </summary>
    public void RegisterActor<TActor>(string? actorTypeName = null)
        where TActor : IActor
    {
        if (actorTypeName is not null && string.IsNullOrWhiteSpace(actorTypeName))
        {
            throw new ArgumentException("Actor type name cannot be empty.", nameof(actorTypeName));
        }

        registrations[typeof(TActor)] = new DaprActorRegistration(typeof(TActor), actorTypeName);
    }

    /// <summary>
    /// Gets the explicit registrations.
    /// </summary>
    public IReadOnlyCollection<DaprActorRegistration> Registrations => registrations.Values;

    /// <summary>
    /// Finds an explicit registration for an actor implementation type.
    /// </summary>
    public DaprActorRegistration? Find(Type actorImplementationType)
    {
        ArgumentNullException.ThrowIfNull(actorImplementationType);
        return registrations.GetValueOrDefault(actorImplementationType);
    }

    internal void CopyFrom(DaprActorRegistrationCollection source)
    {
        ArgumentNullException.ThrowIfNull(source);

        registrations.Clear();
        foreach (var registration in source.registrations)
        {
            registrations[registration.Key] = registration.Value;
        }
    }
}

