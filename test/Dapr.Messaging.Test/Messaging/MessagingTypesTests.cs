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

namespace Dapr.Messaging.Test.Messaging;

/// <summary>
/// Unit tests for the DTOs and constants in <c>Dapr.Messaging.Abstractions</c>.
/// </summary>
public class MessagingTypesTests
{
    // -----------------------------------------------------------------------
    //  CloudEvent
    // -----------------------------------------------------------------------

    [Fact]
    public void CloudEvent_JsonRoundTrip_PreservesProperties()
    {
        var ce = new CloudEvent
        {
            Id = "evt-1",
            Source = new Uri("urn:app:orders"),
            SpecVersion = "1.0",
            Type = "order.created",
            Time = DateTimeOffset.Parse("2026-01-15T10:30:00Z"),
            Subject = "order/42",
            TraceId = "trace-123",
            TraceParent = "00-trace",
            TraceState = "state",
        };

        var json = JsonSerializer.Serialize(ce);
        var deserialized = JsonSerializer.Deserialize<CloudEvent>(json)!;

        Assert.Equal("evt-1", deserialized.Id);
        Assert.Equal(ce.Source, deserialized.Source);
        Assert.Equal("1.0", deserialized.SpecVersion);
        Assert.Equal("order.created", deserialized.Type);
        Assert.Equal(ce.Time, deserialized.Time);
        Assert.Equal("order/42", deserialized.Subject);
        Assert.Equal("trace-123", deserialized.TraceId);
        Assert.Equal("00-trace", deserialized.TraceParent);
        Assert.Equal("state", deserialized.TraceState);
    }

    [Fact]
    public void CloudEvent_JsonPropertyNames_MatchCloudEventSpec()
    {
        var ce = new CloudEvent
        {
            Id = "x",
            Source = new Uri("urn:s"),
            SpecVersion = "1.0",
            Type = "t",
            Time = DateTimeOffset.UtcNow,
            Subject = "sub",
            TraceId = "tid",
            TraceParent = "tp",
            TraceState = "ts",
        };
        var json = JsonSerializer.Serialize(ce);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("source", out _));
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("specversion", out _));
        Assert.True(root.TryGetProperty("time", out _));
        Assert.True(root.TryGetProperty("subject", out _));
        Assert.True(root.TryGetProperty("traceid", out _));
        Assert.True(root.TryGetProperty("traceparent", out _));
        Assert.True(root.TryGetProperty("tracestate", out _));
    }

    [Fact]
    public void CloudEvent_NullStringProperties_OmittedFromJson()
    {
        var json = JsonSerializer.Serialize(new CloudEvent());
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("id", out _));
        Assert.False(doc.RootElement.TryGetProperty("specversion", out _));
    }

    [Fact]
    public void CloudEventTData_DataAndContentType_PreservedAfterConstruction()
    {
        var ce = new CloudEvent<TestOrder>(new TestOrder { Id = "42" })
        {
            Source = new Uri("urn:test"),
            Type = "test",
        };

        Assert.Equal("42", ce.Data.Id);
        Assert.Equal(MessagingConstants.ContentTypeApplicationJson, ce.DataContentType);
    }

    // -----------------------------------------------------------------------
    //  BulkPublishEntry / BulkPublishResponse / BulkPublishResponseFailedEntry
    // -----------------------------------------------------------------------

    [Fact]
    public void BulkPublishEntry_Constructor_SetsProperties()
    {
        var metadata = new Dictionary<string, string> { ["k"] = "v" };
        var entry = new BulkPublishEntry<string>("e1", "data", "text/plain", metadata);

        Assert.Equal("e1", entry.EntryId);
        Assert.Equal("data", entry.EventData);
        Assert.Equal("text/plain", entry.ContentType);
        Assert.Same(metadata, entry.Metadata);
    }

    [Fact]
    public void BulkPublishEntry_NullMetadata_Allowed()
    {
        var entry = new BulkPublishEntry<int>("e1", 42, "application/json");
        Assert.Null(entry.Metadata);
    }

    [Fact]
    public void BulkPublishResponse_Constructor_SetsFailedEntries()
    {
        var failed = new List<BulkPublishResponseFailedEntry<string>>();
        var response = new BulkPublishResponse<string>(failed);

        Assert.Same(failed, response.FailedEntries);
    }

    [Fact]
    public void BulkPublishResponseFailedEntry_Constructor_SetsProperties()
    {
        var entry = new BulkPublishEntry<int>("e1", 1, "application/json");
        var failed = new BulkPublishResponseFailedEntry<int>(entry, "timeout");

        Assert.Same(entry, failed.Entry);
        Assert.Equal("timeout", failed.ErrorMessage);
    }

    // -----------------------------------------------------------------------
    //  PublishOptions
    // -----------------------------------------------------------------------

    [Fact]
    public void PublishOptions_Defaults_MetadataIsEmptyDictionary()
    {
        var opts = new PublishOptions();
        Assert.NotNull(opts.Metadata);
        Assert.Empty(opts.Metadata);
    }

    [Fact]
    public void PublishOptions_Init_SetsAllProperties()
    {
        var opts = new PublishOptions
        {
            ContentType = "application/custom",
            Id = "id-1",
            Source = new Uri("urn:s"),
            Type = "t",
            Subject = "sub",
            TraceParent = "tp",
            TraceState = "ts",
            Metadata = { ["foo"] = "bar" },
        };

        Assert.Equal("application/custom", opts.ContentType);
        Assert.Equal("id-1", opts.Id);
        Assert.Equal("urn:s", opts.Source!.ToString());
        Assert.Equal("t", opts.Type);
        Assert.Equal("sub", opts.Subject);
        Assert.Equal("tp", opts.TraceParent);
        Assert.Equal("ts", opts.TraceState);
        Assert.Equal("bar", opts.Metadata["foo"]);
    }

    // -----------------------------------------------------------------------
    //  MessagingConstants
    // -----------------------------------------------------------------------

    [Fact]
    public void MessagingConstants_HaveExpectedValues()
    {
        Assert.Equal("application/json", MessagingConstants.ContentTypeApplicationJson);
        Assert.Equal("application/grpc", MessagingConstants.ContentTypeApplicationGrpc);
        Assert.Equal("application/cloudevents+json", MessagingConstants.ContentTypeCloudEvent);
        Assert.Equal("application/octet-stream", MessagingConstants.ContentTypeApplicationOctetStream);
    }

    // -----------------------------------------------------------------------
    //  TopicContext
    // -----------------------------------------------------------------------

    [Fact]
    public void TopicContext_Defaults_CollectionsNotNull()
    {
        var ctx = new TopicContext();
        Assert.NotNull(ctx.Metadata);
        Assert.NotNull(ctx.Headers);
        Assert.Empty(ctx.Metadata);
        Assert.Empty(ctx.Headers);
    }

    // -----------------------------------------------------------------------
    //  TopicSubscriptionDescriptor
    // -----------------------------------------------------------------------

    [Fact]
    public void TopicSubscriptionDescriptor_Defaults_CollectionsNotNull()
    {
        var d = new TopicSubscriptionDescriptor();
        Assert.NotNull(d.Metadata);
        Assert.Empty(d.Metadata);
        Assert.Equal(string.Empty, d.Route);
    }

    [Fact]
    public void TopicSubscriptionDescriptor_Route_CanBeSet()
    {
        var d = new TopicSubscriptionDescriptor { Route = "custom/route" };
        Assert.Equal("custom/route", d.Route);
    }

    // -----------------------------------------------------------------------
    //  BulkSubscribeOptions
    // -----------------------------------------------------------------------

    [Fact]
    public void BulkSubscribeOptions_Defaults()
    {
        var opts = new BulkSubscribeOptions();
        Assert.False(opts.Enabled);
        Assert.Equal(100, opts.MaxMessagesCount);
        Assert.Equal(1000, opts.MaxAwaitDurationMs);
    }

    // -----------------------------------------------------------------------
    //  DeadLetterOptions
    // -----------------------------------------------------------------------

    [Fact]
    public void DeadLetterOptions_Init_SetsTopicName()
    {
        var opts = new DeadLetterOptions { TopicName = "orders-dlq" };
        Assert.Equal("orders-dlq", opts.TopicName);
    }

    // -----------------------------------------------------------------------
    //  DeliveryMode enum
    // -----------------------------------------------------------------------

    [Fact]
    public void DeliveryMode_HasThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<DeliveryMode>().Length);
        Assert.True(Enum.IsDefined(DeliveryMode.Streaming));
        Assert.True(Enum.IsDefined(DeliveryMode.Programmatic));
        Assert.True(Enum.IsDefined(DeliveryMode.Http));
    }

    // -----------------------------------------------------------------------
    //  DaprTopicAttribute
    // -----------------------------------------------------------------------

    [Fact]
    public void DaprTopicAttribute_Constructor_SetsPubsubAndTopic()
    {
        var attr = new DaprTopicAttribute("mypubsub", "mytopic");
        Assert.Equal("mypubsub", attr.PubsubName);
        Assert.Equal("mytopic", attr.TopicName);
    }

    [Fact]
    public void DaprTopicAttribute_Defaults_DeliveryIsStreaming()
    {
        var attr = new DaprTopicAttribute("p", "t");
        Assert.Equal(DeliveryMode.Streaming, attr.Delivery);
    }

    [Fact]
    public void DaprTopicAttribute_NamedProperties_Settable()
    {
        var attr = new DaprTopicAttribute("p", "t")
        {
            Delivery = DeliveryMode.Programmatic,
            Route = "my/route",
            Match = "event.type == \"v2\"",
            Priority = 5,
            DeadLetterTopic = "dlq",
            EnableRawPayload = true,
            BulkSubscribe = true,
            MaxMessagesCount = 50,
            MaxAwaitDurationMs = 500,
            MetadataKeys = new[] { "k1" },
        };

        Assert.Equal(DeliveryMode.Programmatic, attr.Delivery);
        Assert.Equal("my/route", attr.Route);
        Assert.Equal("event.type == \"v2\"", attr.Match);
        Assert.Equal(5, attr.Priority);
        Assert.Equal("dlq", attr.DeadLetterTopic);
        Assert.True(attr.EnableRawPayload);
        Assert.True(attr.BulkSubscribe);
        Assert.Equal(50, attr.MaxMessagesCount);
        Assert.Equal(500, attr.MaxAwaitDurationMs);
        Assert.Equal(new[] { "k1" }, attr.MetadataKeys);
    }

    [Fact]
    public void DaprTopicAttribute_AllowMultipleTrue()
    {
        var usage = typeof(DaprTopicAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false).FirstOrDefault() as AttributeUsageAttribute;
        Assert.NotNull(usage);
        Assert.True(usage!.AllowMultiple);
        Assert.False(usage.Inherited);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
    }

    // -----------------------------------------------------------------------
    //  DaprTopicMetadataAttribute
    // -----------------------------------------------------------------------

    [Fact]
    public void DaprTopicMetadataAttribute_Constructor_SetsKeyAndValue()
    {
        var attr = new DaprTopicMetadataAttribute("key", "value");
        Assert.Equal("key", attr.Key);
        Assert.Equal("value", attr.Value);
    }

    [Fact]
    public void DaprTopicMetadataAttribute_AllowMultipleTrue_AssemblyOrClass()
    {
        var usage = typeof(DaprTopicMetadataAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false).FirstOrDefault() as AttributeUsageAttribute;
        Assert.NotNull(usage);
        Assert.True(usage!.AllowMultiple);
        Assert.True((usage.ValidOn & AttributeTargets.Assembly) != 0);
        Assert.True((usage.ValidOn & AttributeTargets.Class) != 0);
    }

    // -----------------------------------------------------------------------
    //  DaprMessagingOptions
    // -----------------------------------------------------------------------

    [Fact]
    public void DaprMessagingOptions_Defaults()
    {
        var opts = new DaprMessagingOptions();
        Assert.Null(opts.DaprApiToken);
        Assert.NotNull(opts.JsonSerializerOptions);
        Assert.Equal("http://localhost:50001", opts.DaprGrpcEndpoint);
        Assert.Equal(TimeSpan.FromSeconds(5), opts.StreamingReconnectDelay);
    }

    public sealed class TestOrder { public string Id { get; set; } = string.Empty; }
}
