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
    private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly IActorStateFaultInjector faultInjector = faultInjector ?? new NoopActorStateFaultInjector();

    /// <inheritdoc />
    public async ValueTask<IActorState<T>?> TryGetAsync<T>(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (entries.TryGetValue(name, out var existing))
        {
            if (existing.Removed)
            {
                return null;
            }

            if (existing.State is IActorState<T> typed)
            {
                return typed;
            }

            var snapshot = existing.PersistedSnapshot ?? existing.CreateSnapshot(serializer);
            return await ReadStateAsync<T>(name, snapshot, cancellationToken).ConfigureAwait(false);
        }

        var bytes = await store.ReadAsync(actorType, actorId.Value, name, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }

        var state = await ReadStateAsync<T>(name, bytes.Value, cancellationToken).ConfigureAwait(false);
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

        var state = new CachedActorState<T>(
            name,
            valueFactory(),
            () => MarkDirty(name),
            ResolveWriteNode<T>(),
            ShouldStorePlain<T>());
        entries[name] = CacheEntry.FromDirty(state, CreateWriter<T>(), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>());
        return state;
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
            ShouldStorePlain<T>());
        entries[name] = CacheEntry.FromDirty(state, CreateWriter<T>(), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>());
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
        entries[name] = CacheEntry.RemovedEntry();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SaveStateAsync(CancellationToken cancellationToken = default) =>
        PersistDirtyEntriesAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask EvictCacheAsync(CancellationToken cancellationToken = default) =>
        EvictCacheAsync(new DaprEvictStateOptions(), cancellationToken);

    /// <inheritdoc />
    public async ValueTask EvictCacheAsync(DaprEvictStateOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.EvictOnDirtyState)
        {
            foreach (var (name, entry) in entries)
            {
                if (entry.Dirty || (entry.TrackInPlaceMutations && await HasInPlaceMutationAsync(entry, cancellationToken).ConfigureAwait(false)))
                {
                    throw new ActorStateCacheDirtyException(
                        name,
                        $"Cannot evict actor state cache because state '{name}' has unpersisted changes.");
                }
            }
        }

        entries.Clear();
    }

    /// <summary>
    /// Persists dirty state entries to the store at the end of an actor turn.
    /// </summary>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        PersistDirtyEntriesAsync(cancellationToken);

    private async ValueTask PersistDirtyEntriesAsync(CancellationToken cancellationToken)
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

    private async ValueTask<CachedActorState<T>> ReadStateAsync<T>(string name, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
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
                    ShouldStorePlain<T>());
                entries[name] = CacheEntry.FromDirty(state, CreateWriter<T>(), CreateSnapshot(state), ShouldTrackInPlaceMutations<T>());
                return state;
            }

            if (header.FormKind == ActorStateFormKind.Plain)
            {
                var value = await ReadPlainAsync<T>(bytes, cancellationToken).ConfigureAwait(false);
                var state = new CachedActorState<T>(name, value, () => MarkDirty(name), null, true);
                entries[name] = CacheEntry.FromClean(state, CreateWriter<T>(), CreateSnapshot(state), bytes.ToArray(), ShouldTrackInPlaceMutations<T>());
                return state;
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
        var legacyState = new CachedActorState<T>(name, legacy, () => MarkDirty(name), null, true);
        entries[name] = CacheEntry.FromClean(legacyState, CreateWriter<T>(), CreateSnapshot(legacyState), bytes.ToArray(), ShouldTrackInPlaceMutations<T>());
        return legacyState;
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
        JsonException? rawJsonException = null;
        try
        {
            var value = serializer.DeserializeFromBytes<T>(bytes);
            if (value is not null)
            {
                return value;
            }
        }
        catch (JsonException ex)
        {
            rawJsonException = ex;
        }
        catch (InvalidCastException)
        {
            throw;
        }

        try
        {
            var legacyEnvelope = serializer.DeserializeFromBytes<LegacyActorStateEnvelope<T>>(bytes)
                ?? throw new InvalidOperationException($"State '{name}' could not be deserialized.");
            return legacyEnvelope.Value;
        }
        catch (JsonException) when (rawJsonException is not null)
        {
            throw rawJsonException;
        }
    }

    private void MarkDirty(string name)
    {
        if (entries.TryGetValue(name, out var entry))
        {
            entries[name] = entry.AsDirty();
        }
    }

    private ActorStateMigrationNode? ResolveWriteNode<T>() =>
        !disableStateMigration ? migrator?.ResolveTargetNode(typeof(T)) : null;

    private bool ShouldStorePlain<T>() => disableStateMigration || migrator?.ResolveTargetNode(typeof(T)) is null;

    private static bool ShouldTrackInPlaceMutations<T>() => !typeof(T).IsValueType;

    private ValueTask<bool> HasInPlaceMutationAsync(CacheEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entry.PersistedSnapshot is null)
        {
            return ValueTask.FromResult(entry.Dirty);
        }

        var currentSnapshot = entry.CreateSnapshot(serializer);
        return ValueTask.FromResult(!currentSnapshot.AsSpan().SequenceEqual(entry.PersistedSnapshot));
    }

    private static Func<IActorWireSerializer, byte[]> CreateSnapshot<T>(CachedActorState<T> state)
    {
        return serializer =>
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
        };
    }

    private static Func<IActorStateStore, IActorStateFaultInjector, string, string, string, byte[], CancellationToken, ValueTask> CreateWriter<T>()
    {
        return async (store, faultInjector, actorType, actorId, name, snapshot, cancellationToken) =>
        {
            await faultInjector.BeforeWriteAsync(typeof(T), actorType, actorId, name, cancellationToken).ConfigureAwait(false);
            await store.WriteAsync(actorType, actorId, name, snapshot, cancellationToken).ConfigureAwait(false);
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
