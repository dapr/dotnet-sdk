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

#nullable enable

using System.Text.Json;
using Dapr.Common.Serialization;
using Google.Protobuf;

namespace Dapr.StateManagement.Test;

/// <summary>
/// Tests that confirm wire-format compatibility between the legacy <c>Dapr.Client</c> state
/// serialization path and the new <c>Dapr.StateManagement</c> path, so that values written by
/// one client can be read by the other without data loss or corruption.
///
/// <para>
/// <b>DaprClient (Dapr.Client) write path</b> (src/Dapr.Client/TypeConverters.cs):
/// <code>
///   JsonSerializer.SerializeToUtf8Bytes&lt;T&gt;(value, options) → ByteString.CopyFrom(bytes)
/// </code>
/// </para>
/// <para>
/// <b>DaprClient read path</b>:
/// <code>
///   JsonSerializer.Deserialize&lt;T&gt;(byteString.Span, options)
/// </code>
/// </para>
/// <para>
/// <b>DaprStateManagementClient write path</b> (DaprStateManagementGrpcClient.SerializeValue):
/// <code>
///   serializer.Serialize&lt;T&gt;(value) → ByteString.CopyFromUtf8(json)
/// </code>
/// </para>
/// <para>
/// <b>DaprStateManagementClient read path</b>:
/// <code>
///   serializer.Deserialize&lt;T&gt;(byteString.ToStringUtf8())
/// </code>
/// </para>
///
/// <para>
/// Both <see cref="Dapr.Client.DaprClientBuilder"/> and
/// <see cref="DaprStateManagementClientBuilder"/> default to
/// <see cref="JsonSerializerDefaults.Web"/>, so the same JSON serializer options govern both
/// clients by default.
/// </para>
/// </summary>
public class SerializationCompatibilityTests
{
    // These options replicate the defaults used by both DaprClientBuilder and
    // DaprStateManagementClientBuilder (both set JsonSerializerDefaults.Web as their default).
    private static readonly JsonSerializerOptions WebOptions =
        new(JsonSerializerDefaults.Web);

    // The serializer used by DaprStateManagementGrpcClient (configured with the same defaults).
    private static readonly IDaprSerializer StateSerializer =
        new JsonDaprSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    // ── helpers that replicate each client's internal serialization step ──────

    /// <summary>Replicates TypeConverters.ToJsonByteString used by DaprClient.</summary>
    private static ByteString DaprClientSerialize<T>(T value) =>
        ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(value, WebOptions));

    /// <summary>Replicates TypeConverters.FromJsonByteString used by DaprClient.</summary>
    private static T? DaprClientDeserialize<T>(ByteString bytes) =>
        bytes.IsEmpty ? default : JsonSerializer.Deserialize<T>(bytes.Span, WebOptions);

    /// <summary>Replicates DaprStateManagementGrpcClient.SerializeValue.</summary>
    private static ByteString StateManagementSerialize<T>(T value) =>
        ByteString.CopyFromUtf8(StateSerializer.Serialize<T>(value));

    /// <summary>Replicates DaprStateManagementGrpcClient's read path in GetStateAsync.</summary>
    private static T? StateManagementDeserialize<T>(ByteString bytes) =>
        StateSerializer.Deserialize<T>(bytes.IsEmpty ? null : bytes.ToStringUtf8());

    // ── Cross-client round-trip: primitives ───────────────────────────────────

    [Fact]
    public void String_WrittenByDaprClient_CanBeReadByStateManagementClient()
    {
        const string value = "hello world";
        var bytes = DaprClientSerialize(value);
        var result = StateManagementDeserialize<string>(bytes);
        Assert.Equal(value, result);
    }

    [Fact]
    public void String_WrittenByStateManagementClient_CanBeReadByDaprClient()
    {
        const string value = "hello world";
        var bytes = StateManagementSerialize(value);
        var result = DaprClientDeserialize<string>(bytes);
        Assert.Equal(value, result);
    }

    [Fact]
    public void Integer_WrittenByDaprClient_CanBeReadByStateManagementClient()
    {
        const int value = 42;
        var bytes = DaprClientSerialize(value);
        var result = StateManagementDeserialize<int>(bytes);
        Assert.Equal(value, result);
    }

    [Fact]
    public void Integer_WrittenByStateManagementClient_CanBeReadByDaprClient()
    {
        const int value = 42;
        var bytes = StateManagementSerialize(value);
        var result = DaprClientDeserialize<int>(bytes);
        Assert.Equal(value, result);
    }

    // ── Cross-client round-trip: complex types ────────────────────────────────

    [Fact]
    public void ComplexObject_WrittenByDaprClient_CanBeReadByStateManagementClient()
    {
        var value = new SampleData { Name = "Alice", Count = 7, IsActive = true };
        var bytes = DaprClientSerialize(value);
        var result = StateManagementDeserialize<SampleData>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Count, result.Count);
        Assert.Equal(value.IsActive, result.IsActive);
    }

    [Fact]
    public void ComplexObject_WrittenByStateManagementClient_CanBeReadByDaprClient()
    {
        var value = new SampleData { Name = "Bob", Count = 99, IsActive = false };
        var bytes = StateManagementSerialize(value);
        var result = DaprClientDeserialize<SampleData>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Count, result.Count);
        Assert.Equal(value.IsActive, result.IsActive);
    }

    // ── Wire format: same bytes ───────────────────────────────────────────────

    /// <summary>
    /// Confirms the on-wire bytes are identical regardless of which client serializes.
    /// Both clients produce the same UTF-8 JSON representation.
    /// </summary>
    [Fact]
    public void BothClients_ProduceSameBytes_ForString()
    {
        const string value = "test value";
        var daprClientBytes = DaprClientSerialize(value);
        var stateManagementBytes = StateManagementSerialize(value);
        Assert.Equal(daprClientBytes, stateManagementBytes);
    }

    [Fact]
    public void BothClients_ProduceSameBytes_ForComplexObject()
    {
        var value = new SampleData { Name = "Carol", Count = 3, IsActive = true };
        var daprClientBytes = DaprClientSerialize(value);
        var stateManagementBytes = StateManagementSerialize(value);
        Assert.Equal(daprClientBytes, stateManagementBytes);
    }

    // ── JsonSerializerDefaults.Web behaviour: camelCase property names ────────

    /// <summary>
    /// Both clients use camelCase serialization (JsonSerializerDefaults.Web).
    /// A value serialized with PascalCase property names in C# should appear as camelCase
    /// on the wire, and the other client should read it back correctly.
    /// </summary>
    [Fact]
    public void CamelCaseNaming_DaprClientWrite_StateManagementRead()
    {
        var value = new SampleData { Name = "Dave", Count = 10, IsActive = true };
        var bytes = DaprClientSerialize(value);
        var json = bytes.ToStringUtf8();

        // Confirm camelCase is used on the wire
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"count\"", json);
        Assert.Contains("\"isActive\"", json);

        // StateManagement client should still deserialize correctly
        var result = StateManagementDeserialize<SampleData>(bytes);
        Assert.Equal(value.Name, result!.Name);
    }

    [Fact]
    public void CamelCaseNaming_StateManagementWrite_DaprClientRead()
    {
        var value = new SampleData { Name = "Eve", Count = 20, IsActive = false };
        var bytes = StateManagementSerialize(value);
        var json = bytes.ToStringUtf8();

        // Confirm camelCase is used on the wire
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"count\"", json);
        Assert.Contains("\"isActive\"", json);

        // DaprClient path should still deserialize correctly
        var result = DaprClientDeserialize<SampleData>(bytes);
        Assert.Equal(value.Name, result!.Name);
    }

    // ── Null / empty value handling ───────────────────────────────────────────

    [Fact]
    public void NullValue_WrittenByDaprClient_ReturnsDefault_WhenReadByStateManagementClient()
    {
        // DaprClient stores JSON "null" bytes for a null reference.
        var bytes = DaprClientSerialize<SampleData?>(null);
        var result = StateManagementDeserialize<SampleData>(bytes);
        Assert.Null(result);
    }

    [Fact]
    public void EmptyBytes_ReturnsDefault_ForBothClients()
    {
        var daprResult = DaprClientDeserialize<SampleData>(ByteString.Empty);
        var stateResult = StateManagementDeserialize<SampleData>(ByteString.Empty);
        Assert.Null(daprResult);
        Assert.Null(stateResult);
    }

    // ── JsonSerializerDefaults.Web: default options match ────────────────────

    /// <summary>
    /// Asserts that both builders start with <see cref="JsonSerializerDefaults.Web"/> defaults,
    /// meaning they share the same camelCase policy and case-insensitive matching by default.
    /// </summary>
    [Fact]
    public void DaprStateManagementClientBuilder_DefaultOptions_UseWebDefaults()
    {
        var builder = new DaprStateManagementClientBuilder();
        Assert.Equal(JsonNamingPolicy.CamelCase, builder.JsonSerializerOptions.PropertyNamingPolicy);
        Assert.True(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    // ── Inheritance serialization ─────────────────────────────────────────────

    /// <summary>
    /// Confirms that a derived type serialized by one client can be deserialized as the
    /// base type by the other client (derived properties are silently dropped, base
    /// properties are preserved).
    /// </summary>
    [Fact]
    public void DerivedType_WrittenByDaprClient_CanBeReadAsBySateManagementClient_AsBaseType()
    {
        var value = new DerivedSampleData { Name = "Alice", Count = 3, IsActive = true, Extra = "bonus" };
        var bytes = DaprClientSerialize(value);
        var result = StateManagementDeserialize<SampleData>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Count, result.Count);
        Assert.Equal(value.IsActive, result.IsActive);
    }

    [Fact]
    public void DerivedType_WrittenByStateManagementClient_CanBeReadByDaprClient_AsBaseType()
    {
        var value = new DerivedSampleData { Name = "Bob", Count = 7, IsActive = false, Extra = "bonus" };
        var bytes = StateManagementSerialize(value);
        var result = DaprClientDeserialize<SampleData>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Count, result.Count);
        Assert.Equal(value.IsActive, result.IsActive);
    }

    [Fact]
    public void DerivedType_WrittenByDaprClient_CanBeReadByStateManagementClient_AsDerivedType()
    {
        var value = new DerivedSampleData { Name = "Carol", Count = 5, IsActive = true, Extra = "extended" };
        var bytes = DaprClientSerialize(value);
        var result = StateManagementDeserialize<DerivedSampleData>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Count, result.Count);
        Assert.Equal(value.IsActive, result.IsActive);
        Assert.Equal(value.Extra, result.Extra);
    }

    [Fact]
    public void DerivedType_WrittenByStateManagementClient_CanBeReadByDaprClient_AsDerivedType()
    {
        var value = new DerivedSampleData { Name = "Dave", Count = 12, IsActive = false, Extra = "extra info" };
        var bytes = StateManagementSerialize(value);
        var result = DaprClientDeserialize<DerivedSampleData>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Count, result.Count);
        Assert.Equal(value.IsActive, result.IsActive);
        Assert.Equal(value.Extra, result.Extra);
    }

    // ── Enum serialization ────────────────────────────────────────────────────

    /// <summary>
    /// Both clients default to numeric enum serialization (JsonSerializerDefaults.Web does not
    /// alter enum handling). Confirms that enum values round-trip correctly across clients.
    /// </summary>
    [Fact]
    public void Enum_WrittenByDaprClient_CanBeReadByStateManagementClient()
    {
        const SampleStatus value = SampleStatus.Active;
        var bytes = DaprClientSerialize(value);
        var result = StateManagementDeserialize<SampleStatus>(bytes);
        Assert.Equal(value, result);
    }

    [Fact]
    public void Enum_WrittenByStateManagementClient_CanBeReadByDaprClient()
    {
        const SampleStatus value = SampleStatus.Inactive;
        var bytes = StateManagementSerialize(value);
        var result = DaprClientDeserialize<SampleStatus>(bytes);
        Assert.Equal(value, result);
    }

    [Fact]
    public void EnumInComplexObject_WrittenByDaprClient_CanBeReadByStateManagementClient()
    {
        var value = new SampleWithEnum { Label = "test", Status = SampleStatus.Pending };
        var bytes = DaprClientSerialize(value);
        var result = StateManagementDeserialize<SampleWithEnum>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Label, result.Label);
        Assert.Equal(value.Status, result.Status);
    }

    [Fact]
    public void EnumInComplexObject_WrittenByStateManagementClient_CanBeReadByDaprClient()
    {
        var value = new SampleWithEnum { Label = "another", Status = SampleStatus.Active };
        var bytes = StateManagementSerialize(value);
        var result = DaprClientDeserialize<SampleWithEnum>(bytes);
        Assert.NotNull(result);
        Assert.Equal(value.Label, result.Label);
        Assert.Equal(value.Status, result.Status);
    }

    [Fact]
    public void BothClients_ProduceSameBytes_ForEnum()
    {
        const SampleStatus value = SampleStatus.Active;
        var daprClientBytes = DaprClientSerialize(value);
        var stateManagementBytes = StateManagementSerialize(value);
        Assert.Equal(daprClientBytes, stateManagementBytes);
    }

    // ── Shared fixture types ──────────────────────────────────────────────────

    private class SampleData
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class DerivedSampleData : SampleData
    {
        public string Extra { get; set; } = string.Empty;
    }

    private enum SampleStatus
    {
        Pending = 0,
        Active = 1,
        Inactive = 2,
    }

    private sealed class SampleWithEnum
    {
        public string Label { get; set; } = string.Empty;
        public SampleStatus Status { get; set; }
    }
}
