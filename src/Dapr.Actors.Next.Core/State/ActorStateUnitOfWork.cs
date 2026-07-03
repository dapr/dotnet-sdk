using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Serialization;

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Activation-scoped write-behind state cache flushed at the end of each actor turn.
/// </summary>
public sealed class ActorStateUnitOfWork(
    string actorType,
    ActorId actorId,
    IActorStateStore store,
    IActorWireSerializer serializer,
    IActorStateFaultInjector? faultInjector = null) : IActorStateAccessor
{
    private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly IActorStateFaultInjector faultInjector = faultInjector ?? new NoopActorStateFaultInjector();

    /// <inheritdoc />
    public async ValueTask<IActorState<T>?> TryGetAsync<T>(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (entries.TryGetValue(name, out var existing))
        {
            return existing.Removed ? null : (IActorState<T>)existing.State;
        }

        var bytes = await store.ReadAsync(actorType, actorId.Value, name, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }

        var envelope = serializer.DeserializeFromBytes<ActorStateEnvelope<T>>(bytes.Value)
            ?? throw new InvalidOperationException($"State '{name}' could not be deserialized.");
        var state = new CachedActorState<T>(name, envelope.SchemaVersion, envelope.Value, () => MarkDirty(name));
        entries[name] = CacheEntry.FromClean(state, CreateWriter(state), CreateSnapshot(state), bytes.Value.ToArray(), ShouldTrackInPlaceMutations<T>());
        return state;
    }

    /// <inheritdoc />
    public async ValueTask<IActorState<T>> GetOrCreateAsync<T>(string name, Func<T> valueFactory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        var existing = await TryGetAsync<T>(name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var state = new CachedActorState<T>(name, 1, valueFactory(), () => MarkDirty(name));
        entries[name] = CacheEntry.FromDirty(state, CreateWriter(state), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>());
        return state;
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(string name, T value, int schemaVersion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var state = new CachedActorState<T>(name, schemaVersion, value, () => MarkDirty(name));
        entries[name] = CacheEntry.FromDirty(state, CreateWriter(state), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        entries[name] = CacheEntry.RemovedEntry();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Flushes dirty state entries to the store.
    /// </summary>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        List<string>? candidates = null;
        foreach (var (name, entry) in entries)
        {
            if (entry.Dirty || entry.TrackInPlaceMutations)
            {
                (candidates ??= new List<string>(entries.Count)).Add(name);
            }
        }

        if (candidates is null)
        {
            return;
        }

        foreach (var name in candidates)
        {
            if (!entries.TryGetValue(name, out var entry))
            {
                continue;
            }

            if (!entry.Dirty && !entry.TrackInPlaceMutations)
            {
                continue;
            }

            if (entry.Removed)
            {
                await store.DeleteAsync(actorType, actorId.Value, name, cancellationToken).ConfigureAwait(false);
                entries.Remove(name);
                continue;
            }

            var currentSnapshot = entry.CreateSnapshot(serializer);
            if (entry.PersistedSnapshot is not null && currentSnapshot.AsSpan().SequenceEqual(entry.PersistedSnapshot))
            {
                entries[name] = entry.AsClean(entry.PersistedSnapshot);
                continue;
            }

            await entry.WriteAsync(store, faultInjector, actorType, actorId.Value, name, currentSnapshot, cancellationToken).ConfigureAwait(false);
            entries[name] = entry.AsClean(currentSnapshot);
        }
    }

    private void MarkDirty(string name)
    {
        if (entries.TryGetValue(name, out var entry))
        {
            entries[name] = entry.AsDirty();
        }
    }

    private static bool ShouldTrackInPlaceMutations<T>() => !typeof(T).IsValueType;

    private static Func<IActorWireSerializer, byte[]> CreateSnapshot<T>(IActorState<T> state)
    {
        return serializer =>
        {
            var envelope = new ActorStateEnvelope<T>(state.SchemaVersion, state.Value);
            return serializer.SerializeToBytes(envelope);
        };
    }

    private static Func<IActorStateStore, IActorStateFaultInjector, string, string, string, byte[], CancellationToken, ValueTask> CreateWriter<T>(IActorState<T> state)
    {
        return async (store, faultInjector, actorType, actorId, name, snapshot, cancellationToken) =>
        {
            await faultInjector.BeforeWriteAsync(typeof(T), actorType, actorId, name, cancellationToken).ConfigureAwait(false);
            await store.WriteAsync(actorType, actorId, name, snapshot, cancellationToken).ConfigureAwait(false);
        };
    }

    private sealed class CacheEntry
    {
        private readonly Func<IActorStateStore, IActorStateFaultInjector, string, string, string, byte[], CancellationToken, ValueTask> writer;
        private readonly Func<IActorWireSerializer, byte[]> snapshot;

        private CacheEntry(
            object state,
            Func<IActorStateStore, IActorStateFaultInjector, string, string, string, byte[], CancellationToken, ValueTask> writer,
            Func<IActorWireSerializer, byte[]> snapshot,
            byte[]? persistedSnapshot,
            bool dirty,
            bool removed,
            bool trackInPlaceMutations)
        {
            State = state;
            this.writer = writer;
            this.snapshot = snapshot;
            PersistedSnapshot = persistedSnapshot;
            Dirty = dirty;
            Removed = removed;
            TrackInPlaceMutations = trackInPlaceMutations;
        }

        public object State { get; }

        public bool Dirty { get; }

        public bool Removed { get; }

        public bool TrackInPlaceMutations { get; }

        public byte[]? PersistedSnapshot { get; }

        public static CacheEntry FromClean(
            object state,
            Func<IActorStateStore, IActorStateFaultInjector, string, string, string, byte[], CancellationToken, ValueTask> writer,
            Func<IActorWireSerializer, byte[]> snapshot,
            byte[] persistedSnapshot,
            bool trackInPlaceMutations) =>
            new(state, writer, snapshot, persistedSnapshot, false, false, trackInPlaceMutations);

        public static CacheEntry FromDirty(
            object state,
            Func<IActorStateStore, IActorStateFaultInjector, string, string, string, byte[], CancellationToken, ValueTask> writer,
            Func<IActorWireSerializer, byte[]> snapshot,
            bool trackInPlaceMutations) =>
            new(state, writer, snapshot, null, true, false, trackInPlaceMutations);

        public static CacheEntry RemovedEntry() => new(new object(), static (_, _, _, _, _, _, _) => ValueTask.CompletedTask, static _ => Array.Empty<byte>(), null, true, true, false);

        public CacheEntry AsDirty() => new(State, writer, snapshot, PersistedSnapshot, true, Removed, TrackInPlaceMutations);

        public CacheEntry AsClean(byte[]? persistedSnapshot) => new(State, writer, snapshot, persistedSnapshot, false, Removed, TrackInPlaceMutations);

        public byte[] CreateSnapshot(IActorWireSerializer serializer) => snapshot(serializer);

        public ValueTask WriteAsync(
            IActorStateStore store,
            IActorStateFaultInjector faultInjector,
            string actorType,
            string actorId,
            string name,
            byte[] currentSnapshot,
            CancellationToken cancellationToken)
        {
            return writer(store, faultInjector, actorType, actorId, name, currentSnapshot, cancellationToken);
        }
    }
}
