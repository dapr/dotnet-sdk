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

using System.Text;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.State.Versioning;
using Dapr.Common.Serialization;

namespace Dapr.Actors.Next.Core.Test;

public sealed class StateCacheRegressionTests
{
    private static readonly ActorId Id = ActorId.Create("one");
    private readonly ActorWireSerializer serializer = new(new JsonDaprSerializer());

    [Fact]
    public async Task Struct_containing_mutable_references_is_tracked_on_save_and_reload()
    {
        var store = new RecordingStore();
        var cache = CreateCache(store);
        await cache.SetAsync("value", new MutableStruct([1]));
        await cache.FlushAsync();
        foreach (var expected in new[] { 2, 3 })
        {
            var state = (await cache.TryGetAsync<MutableStruct>("value"))!;
            state.Value.Values.Add(expected);
            await Assert.ThrowsAsync<ActorStateCacheDirtyException>(() => cache.EvictCacheAsync().AsTask());
            await cache.FlushAsync();
            var writes = store.Writes;
            await cache.FlushAsync();
            Assert.Equal(writes, store.Writes);
            cache = CreateCache(store);
            Assert.Equal(Enumerable.Range(1, expected), (await cache.TryGetAsync<MutableStruct>("value"))!.Value.Values);
        }
    }

    [Theory]
    [InlineData("snake")]
    [InlineData("kebab")]
    [InlineData("custom")]
    [InlineData("pascal")]
    public async Task Envelope_metadata_is_independent_of_payload_naming_policy(string policyName)
    {
        JsonNamingPolicy? policy = policyName switch
        {
            "snake" => JsonNamingPolicy.SnakeCaseLower,
            "kebab" => JsonNamingPolicy.KebabCaseLower,
            "custom" => new PrefixNamingPolicy(),
            _ => null
        };
        var wire = new ActorWireSerializer(new JsonDaprSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = policy,
            WriteIndented = true
        }));
        foreach (var migrator in new IActorStateMigrator?[] { null, CreateMigrator() })
        {
            var store = new RecordingStore();
            ActorStateUnitOfWork Cache() => new("Actor", Id, store, wire, migrator: migrator);
            var cache = Cache();
            await cache.SetAsync("value", new Balance { Amount = 42 });
            await cache.FlushAsync();
            var bytes = (await store.ReadAsync("Actor", Id.Value, "value"))!.Value;
            using var document = JsonDocument.Parse(bytes);
            Assert.Equal(209, document.RootElement.GetProperty("header").GetProperty("magic").GetInt32());
            var restored = Cache();
            Assert.Equal(42, (await restored.TryGetAsync<Balance>("value"))!.Value.Amount);
            if (migrator is null)
            {
                var writes = store.Writes;
                await restored.FlushAsync();
                Assert.Equal(writes, store.Writes);
            }
            if (migrator is not null)
            {
                Assert.Equal("balance-v1", document.RootElement.GetProperty("discriminator").GetProperty("shapeHash").GetString());
                var migrated = Cache();
                Assert.Equal(42, (await migrated.TryGetAsync<MigratedBalance>("value"))!.Value.Total);
                await migrated.FlushAsync();
                Assert.Equal(42, (await Cache().TryGetAsync<MigratedBalance>("value"))!.Value.Total);
            }
        }
    }

    [Theory]
    [InlineData("formatVersion", "\"invalid\"")]
    [InlineData("formatVersion", "2147483648")]
    [InlineData("formatVersion", null)]
    [InlineData("formKind", "\"invalid\"")]
    [InlineData("formKind", "258")]
    [InlineData("formKind", null)]
    [InlineData("serializerId", "42")]
    [InlineData("serializerId", "null")]
    [InlineData("serializerId", null)]
    [InlineData("serializerVersion", "\"invalid\"")]
    [InlineData("serializerVersion", null)]
    public async Task Recognized_header_corruption_never_falls_back_to_raw_state(string property, string? value)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(
            """{"header":{"magic":209,"formatVersion":1,"formKind":2,"serializerId":"dapr-json","serializerVersion":1},"value":{"amount":42}}""")!;
        if (value is null) json["header"]!.AsObject().Remove(property);
        else json["header"]![property] = System.Text.Json.Nodes.JsonNode.Parse(value);
        await Assert.ThrowsAsync<ActorStateEnvelopeException>(() => ReadLegacy<Balance>(json.ToJsonString()));
    }

    [Theory]
    [InlineData("""{"header":{"magic":209,"formatVersion":1,"formKind":2,"serializerId":"dapr-json","serializerVersion":1}}""")]
    [InlineData("""{"header":{"magic":209,"formatVersion":1,"formKind":1,"serializerId":"dapr-json","serializerVersion":1},"value":{"amount":42}}""")]
    [InlineData("""{"header":{"magic":209,"formatVersion":1,"formKind":1,"serializerId":"dapr-json","serializerVersion":1},"discriminator":null,"value":{"amount":42}}""")]
    [InlineData("""{"header":{"magic":209,"formatVersion":1,"formKind":1,"serializerId":"dapr-json","serializerVersion":1},"discriminator":{"chainIndex":"bad","shapeHash":"balance-v1"},"value":{"amount":42}}""")]
    [InlineData("""{"header":{"magic":209,"formatVersion":1,"formKind":1,"serializerId":"dapr-json","serializerVersion":1},"discriminator":{"chainIndex":0},"value":{"amount":42}}""")]
    public async Task Recognized_envelope_requires_payload_and_valid_discriminator(string json) =>
        await Assert.ThrowsAsync<ActorStateEnvelopeException>(() => ReadLegacy<Balance>(json));

    [Theory]
    [InlineData("pascal")]
    [InlineData("snake")]
    [InlineData("kebab")]
    public async Task Previously_written_envelope_metadata_remains_readable(string naming)
    {
        var json = """{"Header":{"Magic":209,"FormatVersion":1,"FormKind":2,"SerializerId":"dapr-json","SerializerVersion":1},"Value":{"Amount":42}}""";
        var policy = naming == "snake" ? JsonNamingPolicy.SnakeCaseLower : JsonNamingPolicy.KebabCaseLower;
        if (naming != "pascal")
            foreach (var name in new[] { "Header", "Magic", "FormatVersion", "FormKind", "SerializerId", "SerializerVersion", "Value" })
                json = json.Replace($"\"{name}\"", $"\"{policy.ConvertName(name)}\"");
        Assert.Equal(42, (await ReadLegacy<Balance>(json))!.Value.Amount);
    }

    [Fact]
    public void Sdk_serializer_roundtrips_nested_actor_ids_and_rejects_invalid_values()
    {
        var message = new ActorIdMessage(Id, [Id, ActorId.Create("two")]);
        var restored = serializer.DeserializeFromBytes<ActorIdMessage>(serializer.SerializeToBytes(message))!;
        Assert.Equal(message.Id, restored.Id);
        Assert.Equal(message.Ids, restored.Ids);
        Assert.Equal(Id, serializer.DeserializeFromBytes<ActorId>(serializer.SerializeToBytes(Id)));
        foreach (var json in new[] { "{}", """{"value":null}""", """{"value":""}""", """{"value":" "}""" })
        {
            Assert.Throws<ArgumentException>(() => serializer.DeserializeFromBytes<ActorId>(Encoding.UTF8.GetBytes(json)));
        }
        Assert.Throws<JsonException>(() => serializer.DeserializeFromBytes<ActorId>(
            Encoding.UTF8.GetBytes("""{"value":42}""")));
    }

    [Fact]
    public async Task Cross_type_plain_read_preserves_dirty_scalars_and_new_entries()
    {
        var store = new RecordingStore();
        var cache = CreateCache(store);
        await cache.SetAsync("value", 1);
        await cache.FlushAsync();
        (await cache.TryGetAsync<int>("value"))!.Value = 42;
        Assert.Equal(42L, (await cache.TryGetAsync<long>("value"))!.Value);
        await cache.FlushAsync();
        Assert.Equal(42L, (await CreateCache(store).TryGetAsync<long>("value"))!.Value);

        await cache.SetAsync("new", 17);
        Assert.Equal(17L, (await cache.TryGetAsync<long>("new"))!.Value);
        await cache.FlushAsync();
        Assert.Equal(17L, (await CreateCache(store).TryGetAsync<long>("new"))!.Value);
    }

    [Fact]
    public async Task Cross_type_clean_read_stays_clean_and_subsequent_in_place_mutation_is_saved()
    {
        var store = new RecordingStore();
        var cache = CreateCache(store);
        await cache.SetAsync("value", new Balance { Amount = 7 });
        await cache.FlushAsync();
        var converted = (await cache.TryGetAsync<ExpandedBalance>("value"))!;
        Assert.Equal(7, converted.Value.Amount);
        var writes = store.Writes;
        await cache.FlushAsync();
        Assert.Equal(writes, store.Writes);
        converted.Value.Amount = 8;
        await cache.FlushAsync();
        Assert.Equal(writes + 1, store.Writes);
        Assert.Equal(8, (await CreateCache(store).TryGetAsync<ExpandedBalance>("value"))!.Value.Amount);
    }

    [Fact]
    public async Task Cross_type_read_preserves_unmarked_in_place_mutations()
    {
        var store = new RecordingStore();
        var cache = CreateCache(store);
        await cache.SetAsync("value", new Balance { Amount = 7 });
        await cache.FlushAsync();
        (await cache.TryGetAsync<Balance>("value"))!.Value.Amount = 42;
        Assert.Equal(42, (await cache.TryGetAsync<ExpandedBalance>("value"))!.Value.Amount);
        await cache.FlushAsync();
        Assert.Equal(42, (await CreateCache(store).TryGetAsync<ExpandedBalance>("value"))!.Value.Amount);
    }

    [Fact]
    public async Task Cross_version_migration_uses_current_dirty_state_not_persisted_state()
    {
        var migrator = CreateMigrator();
        foreach (var mutateInPlace in new[] { false, true })
        {
            var store = new RecordingStore();
            var cache = CreateCache(store, migrator);
            await cache.SetAsync("value", new Balance { Amount = 7 });
            await cache.FlushAsync();
            var current = (await cache.TryGetAsync<Balance>("value"))!;
            if (mutateInPlace)
            {
                current.Value.Amount = 42;
            }
            else
            {
                current.Value = new Balance { Amount = 42 };
            }
            var migrated = (await cache.TryGetAsync<MigratedBalance>("value"))!;
            Assert.Equal(42, migrated.Value.Total);
            await cache.FlushAsync();
            Assert.Equal(42, (await CreateCache(store, migrator).TryGetAsync<MigratedBalance>("value"))!.Value.Total);
        }
    }

    [Fact]
    public async Task Failed_cross_type_conversion_leaves_original_dirty_entry_recoverable()
    {
        var store = new RecordingStore();
        var cache = CreateCache(store);
        await cache.SetAsync("value", 1);
        await cache.FlushAsync();
        (await cache.TryGetAsync<int>("value"))!.Value = 42;
        await Assert.ThrowsAsync<JsonException>(() => cache.TryGetAsync<Balance>("value").AsTask());
        Assert.Equal(42, (await cache.TryGetAsync<int>("value"))!.Value);
        await cache.FlushAsync();
        Assert.Equal(42, (await CreateCache(store).TryGetAsync<int>("value"))!.Value);
    }

    [Fact]
    public async Task Legacy_raw_and_enveloped_classes_scalars_and_null_are_unambiguous()
    {
        foreach (var json in new[] { """{"amount":42}""", """{"schemaVersion":1,"value":{"amount":42}}""",
                     """{"SchemaVersion":1,"Value":{"Amount":42}}""" })
        {
            var state = await ReadLegacy<Balance>(json);
            Assert.Equal(42, state!.Value.Amount);
        }
        foreach (var json in new[] { "42", """{"schemaVersion":1,"value":42}""" })
        {
            Assert.Equal(42, (await ReadLegacy<int>(json))!.Value);
        }
        foreach (var json in new[] { "\"hello\"", """{"schemaVersion":1,"value":"hello"}""" })
        {
            Assert.Equal("hello", (await ReadLegacy<string>(json))!.Value);
        }
        foreach (var json in new[] { """{"schemaVersion":1,"value":null}""" })
        {
            var state = await ReadLegacy<Balance?>(json);
            Assert.NotNull(state);
            Assert.Null(state.Value);
            Assert.Null((await ReadLegacy<int?>(json))!.Value);
        }
        Assert.Equal(42, (await ReadLegacy<ValueOnly>("""{"value":42}"""))!.Value.Value);
    }

    [Fact]
    public async Task Raw_null_keeps_historical_deserialization_error_but_plain_envelope_can_contain_null()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadLegacy<Balance?>("null"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadLegacy<int?>("null"));
        foreach (var migrator in new IActorStateMigrator?[] { null, CreateMigrator() })
        {
            var store = new RecordingStore();
            var header = ActorStateEnvelopeHeader.Create(ActorStateFormKind.Plain, serializer.SerializerId, serializer.SerializerVersion);
            await store.WriteAsync("Actor", Id.Value, "reference",
                serializer.SerializeToBytes(new ActorStatePlainEnvelope<Balance?>(header, null)));
            await store.WriteAsync("Actor", Id.Value, "nullable",
                serializer.SerializeToBytes(new ActorStatePlainEnvelope<int?>(header, null)));
            var cache = CreateCache(store, migrator);
            var reference = await cache.TryGetAsync<Balance?>("reference");
            var nullable = await cache.TryGetAsync<int?>("nullable");
            Assert.NotNull(reference);
            Assert.Null(reference.Value);
            Assert.NotNull(nullable);
            Assert.Null(nullable.Value);
        }
    }

    [Fact]
    public async Task Cross_version_struct_migration_preserves_dirty_value_and_persists_without_mutation_tracking()
    {
        var migrator = new ActorStateMigrationRegistry(
        [
            new ActorStateMigrationFamilyRegistration(
                new ActorStateMigrationFamily("StructBalance",
                    [new(0, typeof(StructBalance), "struct-v1"), new(1, typeof(MigratedStructBalance), "struct-v2")],
                    [new(0, 1)]),
                [
                    new(0, (bytes, wire) => wire.DeserializeFromBytes<ActorStateEnvelope<StructBalance>>(bytes)!.Value),
                    new(1, (bytes, wire) => wire.DeserializeFromBytes<ActorStateEnvelope<MigratedStructBalance>>(bytes)!.Value)
                ],
                [new(0, 1, (state, _) => ValueTask.FromResult<object>(new MigratedStructBalance(((StructBalance)state).Amount)))])
        ]);
        var store = new RecordingStore();
        var cache = CreateCache(store, migrator);
        await cache.SetAsync("value", new StructBalance(7));
        await cache.SaveStateAsync();
        (await cache.TryGetAsync<StructBalance>("value"))!.Value = new StructBalance(99);
        var converted = (await cache.TryGetAsync<MigratedStructBalance>("value"))!;
        Assert.Equal(99, converted.Value.Total);
        var writes = store.Writes;
        await cache.FlushAsync();
        Assert.Equal(writes + 1, store.Writes);
        Assert.Equal(99, (await CreateCache(store, migrator).TryGetAsync<MigratedStructBalance>("value"))!.Value.Total);
        await cache.FlushAsync();
        Assert.Equal(writes + 1, store.Writes);
    }

    [Fact]
    public async Task Legacy_invalid_payloads_do_not_silently_return_default()
    {
        foreach (var json in new[] { "{", """{"schemaVersion":"bad","value":42}""",
                     """{"schemaVersion":1,"value":"bad"}""", """{"amount":42}""", "null" })
        {
            await Assert.ThrowsAnyAsync<JsonException>(() => ReadLegacy<int>(json));
        }
    }

    private async Task<IActorState<T>?> ReadLegacy<T>(string json)
    {
        var store = new RecordingStore();
        await store.WriteAsync("Actor", Id.Value, "value", Encoding.UTF8.GetBytes(json));
        return await CreateCache(store).TryGetAsync<T>("value");
    }

    private ActorStateUnitOfWork CreateCache(RecordingStore store, IActorStateMigrator? migrator = null) =>
        new("Actor", Id, store, serializer, migrator: migrator);

    private static IActorStateMigrator CreateMigrator() => new ActorStateMigrationRegistry(
    [
        new ActorStateMigrationFamilyRegistration(
            new ActorStateMigrationFamily("Balance",
                [new(0, typeof(Balance), "balance-v1"), new(1, typeof(MigratedBalance), "balance-v2")],
                [new(0, 1)]),
            [
                new(0, (bytes, wire) => wire.DeserializeFromBytes<ActorStateEnvelope<Balance>>(bytes)!.Value),
                new(1, (bytes, wire) => wire.DeserializeFromBytes<ActorStateEnvelope<MigratedBalance>>(bytes)!.Value)
            ],
            [new(0, 1, (state, _) => ValueTask.FromResult<object>(new MigratedBalance { Total = ((Balance)state).Amount }))])
    ]);

    public sealed record ActorIdMessage(ActorId Id, ActorId[] Ids);
    public sealed class Balance { public int Amount { get; set; } }
    public sealed class ExpandedBalance { public int Amount { get; set; } public string Label { get; set; } = ""; }
    public sealed class MigratedBalance { public int Total { get; set; } }
    public sealed class ValueOnly { public int Value { get; set; } }
    public readonly record struct StructBalance(int Amount);
    public readonly record struct MutableStruct(List<int> Values);
    private sealed class PrefixNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => "custom_" + name;
    }
    public readonly record struct MigratedStructBalance(int Total);

    private sealed class RecordingStore : IActorStateStore
    {
        private readonly InMemoryActorStateStore inner = new();
        public int Writes { get; private set; }
        public ValueTask ApplyAsync(string actorType, string actorId, IReadOnlyList<ActorStateOperation> operations, CancellationToken cancellationToken = default)
        {
            Writes += operations.Count(operation => operation.Value.HasValue);
            return inner.ApplyAsync(actorType, actorId, operations, cancellationToken);
        }
        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(actorType, actorId, name, cancellationToken);
        public ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            Writes++;
            return inner.WriteAsync(actorType, actorId, name, value, cancellationToken);
        }
        public ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(actorType, actorId, name, cancellationToken);
    }
}
