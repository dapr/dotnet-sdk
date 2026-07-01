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

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Default <see cref="IOutboxMessageFactory"/> that discovers routing information from
/// <see cref="DaprOutboxEventAttribute"/> on the event type and serializes payloads as JSON.
/// </summary>
/// <remarks>
/// When <see cref="DaprOutboxOptions.JsonTypeInfoResolver"/> is set, serialization uses the
/// AOT-safe <see cref="JsonSerializer.SerializeToUtf8Bytes(object?, System.Text.Json.Serialization.Metadata.JsonTypeInfo)"/>
/// overload. Otherwise the reflection-based path is used; callers targeting AOT should either
/// supply a resolver or register a custom factory implementation.
/// </remarks>
public sealed class AttributeOutboxMessageFactory : IOutboxMessageFactory
{
    private readonly DaprOutboxOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeOutboxMessageFactory"/> class.
    /// </summary>
    /// <param name="options">The outbox options snapshot.</param>
    public AttributeOutboxMessageFactory(IOptions<DaprOutboxOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Value;
    }

    /// <inheritdoc />
    public OutboxMessage CreateFromDomainEvent(object domainEvent, DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(dbContext);

        var eventType = domainEvent.GetType();
        var attribute = eventType.GetCustomAttribute<DaprOutboxEventAttribute>()
            ?? throw new InvalidOperationException(
                $"Domain event type '{eventType.FullName}' must be decorated with [DaprOutboxEvent(pubSubName, topic)] " +
                "to be published by the default AttributeOutboxMessageFactory, or you must register a custom IOutboxMessageFactory.");

        var payload = Serialize(domainEvent, eventType);
        var metadata = BuildAttributeMetadata(attribute, eventType);

        return new OutboxMessage
        {
            PubSubName = attribute.PubSubName,
            Topic = attribute.Topic,
            ContentType = "application/json",
            Payload = payload,
            MetadataJson = metadata,
        };
    }

    /// <inheritdoc />
    public OutboxMessage CreateFromExplicit(
        string pubSubName,
        string topic,
        object payload,
        IReadOnlyDictionary<string, string>? metadata,
        string? correlationId,
        DbContext dbContext)
    {
        ArgumentException.ThrowIfNullOrEmpty(pubSubName);
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(dbContext);

        var payloadBytes = Serialize(payload, payload.GetType());
        var metadataJson = metadata is { Count: > 0 } ? SerializeMetadata(metadata) : null;

        return new OutboxMessage
        {
            PubSubName = pubSubName,
            Topic = topic,
            ContentType = "application/json",
            Payload = payloadBytes,
            MetadataJson = metadataJson,
            CorrelationId = correlationId,
        };
    }

    [UnconditionalSuppressMessage(
        "Trimming", "IL2026",
        Justification = "Reflection-based serialization is only used when DaprOutboxOptions.JsonTypeInfoResolver is null. " +
                       "AOT/trimmed applications must supply a resolver or register a custom IOutboxMessageFactory.")]
    [UnconditionalSuppressMessage(
        "AOT", "IL3050",
        Justification = "Reflection-based serialization is only used when DaprOutboxOptions.JsonTypeInfoResolver is null. " +
                       "AOT applications must supply a resolver or register a custom IOutboxMessageFactory.")]
    private byte[] Serialize(object payload, Type payloadType)
    {
        if (options.JsonTypeInfoResolver is not null)
        {
            var serializerOptions = options.JsonSerializerOptions is not null
                ? new JsonSerializerOptions(options.JsonSerializerOptions) { TypeInfoResolver = options.JsonTypeInfoResolver }
                : new JsonSerializerOptions { TypeInfoResolver = options.JsonTypeInfoResolver };
            var typeInfo = serializerOptions.GetTypeInfo(payloadType);
            return JsonSerializer.SerializeToUtf8Bytes(payload, typeInfo);
        }

        return JsonSerializer.SerializeToUtf8Bytes(payload, payloadType, options.JsonSerializerOptions);
    }

    [UnconditionalSuppressMessage(
        "Trimming", "IL2026",
        Justification = "Dictionary<string,string> shape is preserved by the framework and always serializes without reflection over user types.")]
    [UnconditionalSuppressMessage(
        "AOT", "IL3050",
        Justification = "Dictionary<string,string> serialization is AOT-safe.")]
    private static string SerializeMetadata(IReadOnlyDictionary<string, string> metadata)
        => JsonSerializer.Serialize(metadata);

    private static string? BuildAttributeMetadata(DaprOutboxEventAttribute attribute, Type eventType)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DaprOutboxMetadata.CloudEventType] = attribute.CloudEventType ?? eventType.Name,
        };

        if (attribute.CloudEventSource is not null)
        {
            merged[DaprOutboxMetadata.CloudEventSource] = attribute.CloudEventSource;
        }

        return SerializeMetadata(merged);
    }
}
