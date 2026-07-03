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
        var interfaceTypeSnapshot = new Dictionary<Type, IReadOnlyList<ActorRuntimeRegistration>>();

        foreach (var registration in registrations)
        {
            ResolveRegistration(registration);
            actorTypeSnapshot.Add(registration.ActorType, registration);
            AddInterfaceRegistration(interfaceTypeSnapshot, registration);
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
    /// Gets registrations by actor interface type.
    /// </summary>
    public IReadOnlyList<ActorRuntimeRegistration> GetAllByInterfaceType(Type interfaceType) =>
        TryGetAllByInterfaceType(interfaceType, out var registrations)
            ? registrations
            : throw new InvalidOperationException($"Actor interface '{interfaceType.FullName}' is not registered.");

    internal bool TryAdd(ActorRuntimeRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (syncRoot)
        {
            var current = snapshot;
            if (current.ByActorType.ContainsKey(registration.ActorType))
            {
                return false;
            }

            ResolveRegistration(registration);

            var nextByActorType = new Dictionary<string, ActorRuntimeRegistration>(current.ByActorType, StringComparer.Ordinal)
            {
                [registration.ActorType] = registration,
            };
            var nextByInterfaceType = new Dictionary<Type, IReadOnlyList<ActorRuntimeRegistration>>(current.ByInterfaceType);
            AddInterfaceRegistration(nextByInterfaceType, registration);
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
            var nextByInterfaceType = new Dictionary<Type, IReadOnlyList<ActorRuntimeRegistration>>(current.ByInterfaceType);
            nextByActorType.Remove(actorType);
            RemoveInterfaceRegistration(nextByInterfaceType, registration);
            Volatile.Write(ref snapshot, new RegistrySnapshot(nextByActorType, nextByInterfaceType));
            return true;
        }
    }

    private bool TryGetByActorType(string actorType, out ActorRuntimeRegistration registration)
    {
        return Volatile.Read(ref snapshot).ByActorType.TryGetValue(actorType, out registration!);
    }

    private bool TryGetAllByInterfaceType(Type interfaceType, out IReadOnlyList<ActorRuntimeRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        return Volatile.Read(ref snapshot).ByInterfaceType.TryGetValue(interfaceType, out registrations!);
    }

    private void ResolveRegistration(ActorRuntimeRegistration registration)
    {
        registration.ResolveDispatcher(services);
    }

    private static void AddInterfaceRegistration(Dictionary<Type, IReadOnlyList<ActorRuntimeRegistration>> registrations, ActorRuntimeRegistration registration)
    {
        if (!registrations.TryGetValue(registration.InterfaceType, out var existing))
        {
            registrations[registration.InterfaceType] = [registration];
            return;
        }

        registrations[registration.InterfaceType] = existing.Concat([registration]).ToArray();
    }

    private static void RemoveInterfaceRegistration(Dictionary<Type, IReadOnlyList<ActorRuntimeRegistration>> registrations, ActorRuntimeRegistration registration)
    {
        if (!registrations.TryGetValue(registration.InterfaceType, out var existing))
        {
            return;
        }

        var remaining = existing.Where(item => !StringComparer.Ordinal.Equals(item.ActorType, registration.ActorType)).ToArray();
        if (remaining.Length == 0)
        {
            registrations.Remove(registration.InterfaceType);
            return;
        }

        registrations[registration.InterfaceType] = remaining;
    }

    private sealed record RegistrySnapshot(
        Dictionary<string, ActorRuntimeRegistration> ByActorType,
        Dictionary<Type, IReadOnlyList<ActorRuntimeRegistration>> ByInterfaceType);
}
