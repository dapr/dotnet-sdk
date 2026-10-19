// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Text.Json;
using System.Collections.Concurrent;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.Serialization;

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Activation-scoped write-behind state cache persisted at the end of each actor turn.
/// </summary>
public sealed class ActorStateUnitOfWork(
    string actorType,
    ActorId actorId,
    IActorStateStore store,
    IActorWireSerializer serializer,
    IActorStateFaultInjector? faultInjector = null,
    IActorStateMigrator? migrator = null,
    bool disableStateMigration = false) : IActorStateAccessor
{
    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim persistGate = new(1, 1);
    private readonly object cacheLock = new();
    private readonly IActorStateFaultInjector faultInjector = faultInjector ?? new NoopActorStateFaultInjector();

    private CacheEntry GetEntry(string name)
    {
        lock (cacheLock)
            return entries.GetOrAdd(name, static _ => CacheEntry.UnloadedEntry());
    }

    private bool ReplaceEntry(string name, CacheEntry replacement, CacheEntry expected)
    {
        lock (cacheLock)
            return entries.TryUpdate(name, replacement, expected);
    }

    private void SetEntry(string name, CacheEntry entry)
    {
        lock (cacheLock) entries[name] = entry;
    }

    /// <inheritdoc />
    public async ValueTask<IActorState<T>?> TryGetAsync<T>(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = GetEntry(name);
            if (existing.Removed || existing.Missing)
            {
                return null;
            }

            if (existing.State is IActorState<T> typed)
            {
                return typed;
            }

            var snapshot = existing.Unloaded ? null : existing.CreateSnapshot(serializer);
            var dirty = existing.Dirty || (existing.PersistedSnapshot is not null
                && !snapshot.AsSpan().SequenceEqual(existing.PersistedSnapshot));
            var bytes = snapshot is not null
                ? new ReadOnlyMemory<byte>(snapshot)
                : await store.ReadAsync(actorType, actorId.Value, name, cancellationToken).ConfigureAwait(false);
            var entry = bytes.HasValue
                ? await ReadStateAsync<T>(name, bytes.Value, cancellationToken).ConfigureAwait(false)
                : CacheEntry.MissingEntry();
            // Conversion must not acknowledge unsaved changes as durable. For clean plain
            // conversions, compare future in-place mutations against the converted shape.
            entry = dirty
                ? entry.AsClean(null).AsDirty()
                : snapshot is not null && !entry.Dirty ? entry.AsClean(entry.CreateSnapshot(serializer)) : entry;
            if (snapshot is not null && !snapshot.AsSpan().SequenceEqual(existing.CreateSnapshot(serializer)))
                continue;
            if (ReplaceEntry(name, entry, existing))
                return entry.Missing ? null : (IActorState<T>)entry.State;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IActorState<T>> GetOrCreateAsync<T>(string name, Func<T> valueFactory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        while (true)
        {
            var existing = await TryGetAsync<T>(name, cancellationToken).ConfigureAwait(false);
            if (existing is not null) return existing;
            if (!entries.TryGetValue(name, out var missing) || (!missing.Missing && !missing.Removed)) continue;
            var state = new CachedActorState<T>(name, valueFactory(), () => MarkDirty(name), ResolveWriteNode<T>(), ShouldStorePlain<T>(), cacheLock);
            if (ReplaceEntry(name, CacheEntry.FromDirty(state, CreateWriter<T>(), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>()), missing))
                return state;
        }
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(string name, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var state = new CachedActorState<T>(
            name,
            value,
            () => MarkDirty(name),
            ResolveWriteNode<T>(),
            ShouldStorePlain<T>(), cacheLock);
        SetEntry(name, CacheEntry.FromDirty(state, CreateWriter<T>(), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>()));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask GraduateAsync<T>(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var state = await TryGetAsync<T>(name, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return;
        }

        ((CachedActorState<T>)state).StoreAsPlain();
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        SetEntry(name, CacheEntry.RemovedEntry());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SaveStateAsync(CancellationToken cancellationToken = default) =>
        PersistDirtyEntriesAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask EvictCacheAsync(CancellationToken cancellationToken = default) =>
        EvictCacheAsync(new DaprEvictStateOptions(), cancellationToken);

    /// <inheritdoc />
    public ValueTask EvictCacheAsync(DaprEvictStateOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        lock (cacheLock)
        {
            if (!options.EvictOnDirtyState)
            {
                foreach (var (name, entry) in entries)
                {
                    if (entry.Dirty || (entry.TrackInPlaceMutations && HasInPlaceMutation(entry)))
                    {
                        throw new ActorStateCacheDirtyException(
                            name,
                            $"Cannot evict actor state cache because state '{name}' has unpersisted changes.");
                    }
                }
            }

            entries.Clear();
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Persists dirty state entries to the store at the end of an actor turn.
    /// </summary>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        PersistDirtyEntriesAsync(cancellationToken);

    private async ValueTask PersistDirtyEntriesAsync(CancellationToken cancellationToken)
    {
        // Serialize commits, not cache access: a reentrant child may be saving while
        // its parent replaces a value needed to release the child's store call.
        await persistGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var batch = new List<(string Name, CacheEntry Entry, byte[]? Snapshot)>();
            lock (cacheLock)
            {
                foreach (var (name, entry) in entries.ToArray())
                {
                    if (!entry.Dirty && !entry.TrackInPlaceMutations) continue;
                    var snapshot = entry.Removed ? null : entry.CreateSnapshot(serializer);
                    if (!entry.Removed && entry.PersistedSnapshot is not null && snapshot.AsSpan().SequenceEqual(entry.PersistedSnapshot))
                    {
                        ReplaceEntry(name, entry.AsClean(entry.PersistedSnapshot), entry);
                        continue;
                    }
                    batch.Add((name, entry, snapshot));
                }
            }

            // All fault hooks run before submitting the transaction, so a fault at
            // any key leaves the entire batch dirty and durable state unchanged.
            foreach (var item in batch)
            {
                if (item.Snapshot is not null)
                    await item.Entry.WriteAsync(store, faultInjector, actorType, actorId.Value, item.Name, item.Snapshot, cancellationToken).ConfigureAwait(false);
            }
            if (batch.Count == 0) return;
            var operations = batch.Select(item => new ActorStateOperation(item.Name,
                item.Snapshot is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(item.Snapshot))).ToArray();
            await faultInjector.BeforeCommitAsync(actorType, actorId.Value, operations, cancellationToken).ConfigureAwait(false);
            await store.ApplyAsync(actorType, actorId.Value, operations, cancellationToken).ConfigureAwait(false);
            foreach (var item in batch)
            {
                if (item.Entry.Removed)
                {
                    lock (cacheLock)
                        ((ICollection<KeyValuePair<string, CacheEntry>>)entries).Remove(new(item.Name, item.Entry));
                }
                else
                    ReplaceEntry(item.Name, item.Entry.AsClean(item.Snapshot), item.Entry);
            }
        }
        finally
        {
            persistGate.Release();
        }
    }

    private async ValueTask<CacheEntry> ReadStateAsync<T>(string name, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (TryPeekHeader(bytes, out var header, out var discriminator))
        {
            ValidateSerializer(header);
            if (header.FormKind == ActorStateFormKind.Enveloped)
            {
                if (migrator is null)
                {
                    throw new ActorStateMigrationException(
                        $"State '{name}' is enrolled for migration, but no actor state migrator is registered.",
                        familyName: null,
                        chainIndex: discriminator.ChainIndex,
                        targetType: typeof(T),
                        shapeHash: discriminator.ShapeHash);
                }

                await faultInjector.BeforeMigrationAsync(typeof(T), actorType, actorId.Value, name, cancellationToken).ConfigureAwait(false);
                var value = await migrator.MigrateAsync<T>(
                    discriminator.ChainIndex,
                    discriminator.ShapeHash,
                    bytes,
                    serializer,
                    (fromStateType, toStateType, token) => faultInjector.BeforeUpcastHopAsync(
                        fromStateType,
                        toStateType,
                        actorType,
                        actorId.Value,
                        name,
                        token),
                    cancellationToken).ConfigureAwait(false);
                var state = new CachedActorState<T>(
                    name,
                    value,
                    () => MarkDirty(name),
                    ResolveWriteNode<T>(),
                    ShouldStorePlain<T>(), cacheLock);
                return CacheEntry.FromDirty(state, CreateWriter<T>(), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>());
            }

            if (header.FormKind == ActorStateFormKind.Plain)
            {
                var value = await ReadPlainAsync<T>(bytes, cancellationToken).ConfigureAwait(false);
                var state = new CachedActorState<T>(name, value, () => MarkDirty(name), null, true, cacheLock);
                return CacheEntry.FromClean(state, CreateWriter<T>(), CreateSnapshot(state), bytes.ToArray(), ShouldTrackInPlaceMutations<T>());
            }

            throw new ActorStateEnvelopeException(
                $"State '{name}' has unsupported actor state form '{header.FormKind}'.",
                name,
                header.FormatVersion,
                header.FormKind.ToString(),
                header.SerializerId,
                header.SerializerVersion,
                serializer.SerializerId,
                serializer.SerializerVersion);
        }

        var legacy = ReadLegacy<T>(name, bytes);
        var legacyState = new CachedActorState<T>(name, legacy, () => MarkDirty(name), null, true, cacheLock);
        return CacheEntry.FromClean(legacyState, CreateWriter<T>(), CreateSnapshot(legacyState), bytes.ToArray(), ShouldTrackInPlaceMutations<T>());
    }

    private async ValueTask<T> ReadPlainAsync<T>(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (migrator is not null)
        {
            return await migrator.ReadPlainAsync<T>(bytes, serializer, cancellationToken).ConfigureAwait(false);
        }

        var envelope = serializer.DeserializeFromBytes<ActorStatePlainEnvelope<T>>(bytes)
            ?? throw new ActorStateEnvelopeException($"State could not be deserialized as '{typeof(T).FullName}'.", stateName: null);
        return envelope.Value;
    }

    private T ReadLegacy<T>(string name, ReadOnlyMemory<byte> bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && TryGetProperty(document.RootElement, nameof(LegacyActorStateEnvelope<T>.SchemaVersion), out var version)
            && TryGetProperty(document.RootElement, nameof(LegacyActorStateEnvelope<T>.Value), out _))
        {
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out _))
            {
                throw new JsonException("Legacy actor state schema version must be an integer.");
            }

            var envelope = serializer.DeserializeFromBytes<LegacyActorStateEnvelope<T>>(bytes)
                ?? throw new InvalidOperationException($"State '{name}' could not be deserialized.");
            return envelope.Value;
        }

        return serializer.DeserializeFromBytes<T>(bytes)
            ?? throw new InvalidOperationException($"State '{name}' could not be deserialized.");
    }

    private void MarkDirty(string name)
    {
        while (entries.TryGetValue(name, out var entry))
        {
            if (entry.Missing || entry.Unloaded || entry.Removed) return;
            if (ReplaceEntry(name, entry.AsDirty(), entry)) return;
        }
    }

    private ActorStateMigrationNode? ResolveWriteNode<T>() =>
        !disableStateMigration ? migrator?.ResolveTargetNode(typeof(T)) : null;

    private bool ShouldStorePlain<T>() => disableStateMigration || migrator?.ResolveTargetNode(typeof(T)) is null;

    private static bool ShouldTrackInPlaceMutations<T>() => !typeof(T).IsValueType;

    private bool HasInPlaceMutation(CacheEntry entry)
    {
        if (entry.PersistedSnapshot is null)
        {
            return entry.Dirty;
        }

        var currentSnapshot = entry.CreateSnapshot(serializer);
        return !currentSnapshot.AsSpan().SequenceEqual(entry.PersistedSnapshot);
    }

    private static Func<IActorWireSerializer, byte[]> CreateSnapshot<T>(CachedActorState<T> state)
    {
        return serializer =>
        {
            lock (state.SyncRoot)
            {
                if (state.MigrationNode is not null && !state.StorePlain)
                {
                    var envelope = new ActorStateEnvelope<T>(
                        ActorStateEnvelopeHeader.Create(ActorStateFormKind.Enveloped, serializer.SerializerId, serializer.SerializerVersion),
                        new ActorStateDiscriminator(state.MigrationNode.Index, state.MigrationNode.ShapeHash),
                        state.Value);
                    return serializer.SerializeToBytes(envelope);
                }

                if (state.StorePlain)
                {
                    var envelope = new ActorStatePlainEnvelope<T>(
                        ActorStateEnvelopeHeader.Create(ActorStateFormKind.Plain, serializer.SerializerId, serializer.SerializerVersion),
                        state.Value);
                    return serializer.SerializeToBytes(envelope);
                }

                return serializer.SerializeToBytes(state.Value);
            }
        };
    }

    private static Func<IActorStateStore, IActorStateFaultInjector, string, string, string, byte[], CancellationToken, ValueTask> CreateWriter<T>()
    {
        return async (store, faultInjector, actorType, actorId, name, snapshot, cancellationToken) =>
        {
            await faultInjector.BeforeWriteAsync(typeof(T), actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        };
    }

    private void ValidateSerializer(ActorStateEnvelopeHeader header)
    {
        if (header.FormatVersion != ActorStateEnvelopeHeader.CurrentFormatVersion)
        {
            throw new ActorStateEnvelopeException(
                $"Unsupported actor state envelope format version '{header.FormatVersion}'.",
                stateName: null,
                formatVersion: header.FormatVersion,
                formKind: header.FormKind.ToString(),
                storedSerializerId: header.SerializerId,
                storedSerializerVersion: header.SerializerVersion,
                currentSerializerId: serializer.SerializerId,
                currentSerializerVersion: serializer.SerializerVersion);
        }

        if (!string.Equals(header.SerializerId, serializer.SerializerId, StringComparison.Ordinal) || header.SerializerVersion != serializer.SerializerVersion)
        {
            throw new ActorStateEnvelopeException(
                $"Actor state serializer mismatch. Stored '{header.SerializerId}' v{header.SerializerVersion}, current '{serializer.SerializerId}' v{serializer.SerializerVersion}.",
                stateName: null,
                formatVersion: header.FormatVersion,
                formKind: header.FormKind.ToString(),
                storedSerializerId: header.SerializerId,
                storedSerializerVersion: header.SerializerVersion,
                currentSerializerId: serializer.SerializerId,
                currentSerializerVersion: serializer.SerializerVersion);
        }
    }

    private static bool TryPeekHeader(ReadOnlyMemory<byte> bytes, out ActorStateEnvelopeHeader header, out ActorStateDiscriminator discriminator)
    {
        header = default;
        discriminator = default;

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryGetProperty(document.RootElement, nameof(ActorStateEnvelope<object>.Header), out var headerElement)
                || headerElement.ValueKind != JsonValueKind.Object
                || !TryReadByte(headerElement, nameof(ActorStateEnvelopeHeader.Magic), out var magic)
                || magic != ActorStateEnvelopeHeader.CurrentMagic)
            {
                return false;
            }

            var formatVersion = GetProperty(headerElement, nameof(ActorStateEnvelopeHeader.FormatVersion)).GetInt32();
            var formKind = ReadFormKind(GetProperty(headerElement, nameof(ActorStateEnvelopeHeader.FormKind)));
            var serializerId = GetProperty(headerElement, nameof(ActorStateEnvelopeHeader.SerializerId)).GetString() ?? string.Empty;
            var serializerVersion = GetProperty(headerElement, nameof(ActorStateEnvelopeHeader.SerializerVersion)).GetInt32();
            header = new ActorStateEnvelopeHeader(magic, formatVersion, formKind, serializerId, serializerVersion);

            if (formKind == ActorStateFormKind.Enveloped)
            {
                var discriminatorElement = GetProperty(document.RootElement, nameof(ActorStateEnvelope<object>.Discriminator));
                discriminator = new ActorStateDiscriminator(
                    GetProperty(discriminatorElement, nameof(ActorStateDiscriminator.ChainIndex)).GetInt32(),
                    GetProperty(discriminatorElement, nameof(ActorStateDiscriminator.ShapeHash)).GetString() ?? string.Empty);
            }

            return true;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool TryReadByte(JsonElement element, string propertyName, out byte value)
    {
        value = default;
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetByte(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String && byte.TryParse(property.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property)
            ? property
            : throw new KeyNotFoundException(propertyName);

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        var camelCase = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        return element.TryGetProperty(camelCase, out property);
    }

    private static ActorStateFormKind ReadFormKind(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return (ActorStateFormKind)element.GetInt32();
        }

        if (element.ValueKind == JsonValueKind.String && Enum.TryParse<ActorStateFormKind>(element.GetString(), ignoreCase: false, out var formKind))
        {
            return formKind;
        }

        throw new InvalidOperationException("Actor state header has an invalid form kind.");
    }

    private sealed record LegacyActorStateEnvelope<T>(int SchemaVersion, T Value);

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
            bool trackInPlaceMutations,
            bool missing = false)
        {
            State = state;
            this.writer = writer;
            this.snapshot = snapshot;
            PersistedSnapshot = persistedSnapshot;
            Dirty = dirty;
            Removed = removed;
            TrackInPlaceMutations = trackInPlaceMutations;
            Missing = missing;
        }

        public object State { get; }

        public bool Dirty { get; }

        public bool Removed { get; }

        public bool Missing { get; }
        public bool Unloaded { get; private init; }

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

        public static CacheEntry MissingEntry() => new(new object(), static (_, _, _, _, _, _, _) => ValueTask.CompletedTask, static _ => Array.Empty<byte>(), null, false, false, false, true);
        public static CacheEntry UnloadedEntry() => new(new object(), static (_, _, _, _, _, _, _) => ValueTask.CompletedTask, static _ => Array.Empty<byte>(), null, false, false, false) { Unloaded = true };

        public CacheEntry AsDirty() => new(State, writer, snapshot, PersistedSnapshot, true, Removed, TrackInPlaceMutations, Missing);

        public CacheEntry AsClean(byte[]? persistedSnapshot) => new(State, writer, snapshot, persistedSnapshot, false, Removed, TrackInPlaceMutations, Missing);

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
