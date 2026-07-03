namespace Dapr.Actors.Next.Core.Registration;

/// <summary>
/// Stores generated actor registrations used by the runtime host.
/// </summary>
public sealed class ActorRuntimeRegistry
{
    private RegistrySnapshot snapshot;
    private readonly object syncRoot = new();
    private readonly IServiceProvider services;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorRuntimeRegistry"/> class.
    /// </summary>
    public ActorRuntimeRegistry(IEnumerable<ActorRuntimeRegistration> registrations, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(services);
        this.services = services;
        var actorTypeSnapshot = new Dictionary<string, ActorRuntimeRegistration>(StringComparer.Ordinal);
        var interfaceTypeSnapshot = new Dictionary<Type, ActorRuntimeRegistration>();

        foreach (var registration in registrations)
        {
            ResolveRegistration(registration);
            actorTypeSnapshot.Add(registration.ActorType, registration);
            interfaceTypeSnapshot.Add(registration.InterfaceType, registration);
        }

        snapshot = new RegistrySnapshot(actorTypeSnapshot, interfaceTypeSnapshot);
    }

    /// <summary>
    /// Gets registered actor type names.
    /// </summary>
    public IReadOnlyList<string> ActorTypes
    {
        get
        {
            return Volatile.Read(ref snapshot).ByActorType.Keys.ToArray();
        }
    }

    /// <summary>
    /// Gets a registration by actor type.
    /// </summary>
    public ActorRuntimeRegistration GetByActorType(string actorType) =>
        TryGetByActorType(actorType, out var registration)
            ? registration
            : throw new InvalidOperationException($"Actor type '{actorType}' is not registered.");

    /// <summary>
    /// Gets a registration by interface type.
    /// </summary>
    public ActorRuntimeRegistration GetByInterfaceType(Type interfaceType) =>
        TryGetByInterfaceType(interfaceType, out var registration)
            ? registration
            : throw new InvalidOperationException($"Actor interface '{interfaceType.FullName}' is not registered.");

    internal bool TryAdd(ActorRuntimeRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (syncRoot)
        {
            var current = snapshot;
            if (current.ByActorType.ContainsKey(registration.ActorType) || current.ByInterfaceType.ContainsKey(registration.InterfaceType))
            {
                return false;
            }

            ResolveRegistration(registration);

            var nextByActorType = new Dictionary<string, ActorRuntimeRegistration>(current.ByActorType, StringComparer.Ordinal)
            {
                [registration.ActorType] = registration,
            };
            var nextByInterfaceType = new Dictionary<Type, ActorRuntimeRegistration>(current.ByInterfaceType)
            {
                [registration.InterfaceType] = registration,
            };
            Volatile.Write(ref snapshot, new RegistrySnapshot(nextByActorType, nextByInterfaceType));
            return true;
        }
    }

    internal bool TryRemove(string actorType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        lock (syncRoot)
        {
            var current = snapshot;
            if (!current.ByActorType.TryGetValue(actorType, out var registration))
            {
                return false;
            }

            var nextByActorType = new Dictionary<string, ActorRuntimeRegistration>(current.ByActorType, StringComparer.Ordinal);
            var nextByInterfaceType = new Dictionary<Type, ActorRuntimeRegistration>(current.ByInterfaceType);
            nextByActorType.Remove(actorType);
            nextByInterfaceType.Remove(registration.InterfaceType);
            Volatile.Write(ref snapshot, new RegistrySnapshot(nextByActorType, nextByInterfaceType));
            return true;
        }
    }

    private bool TryGetByActorType(string actorType, out ActorRuntimeRegistration registration)
    {
        return Volatile.Read(ref snapshot).ByActorType.TryGetValue(actorType, out registration!);
    }

    private bool TryGetByInterfaceType(Type interfaceType, out ActorRuntimeRegistration registration)
    {
        return Volatile.Read(ref snapshot).ByInterfaceType.TryGetValue(interfaceType, out registration!);
    }

    private void ResolveRegistration(ActorRuntimeRegistration registration)
    {
        registration.ResolveDispatcher(services);
    }

    private sealed record RegistrySnapshot(
        Dictionary<string, ActorRuntimeRegistration> ByActorType,
        Dictionary<Type, ActorRuntimeRegistration> ByInterfaceType);
}
