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

using Dapr.Common;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Google.Protobuf;
using Grpc.Core;
using Moq;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.Test.PublishSubscribe;

/// <summary>
/// Unit tests for the publish-side surface of <see cref="DaprPublishSubscribeGrpcClient"/>.
/// </summary>
public class DaprPublishSubscribeGrpcClientPublishTests
{
    private const string PubSubName = "testPubSub";
    private const string TopicName = "testTopic";

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    private static AsyncUnaryCall<T> CreateUnaryCall<T>(T response) =>
        new(Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static AsyncUnaryCall<T> CreateFailingUnaryCall<T>(StatusCode code, string detail) =>
        new(Task.FromException<T>(new RpcException(new Status(code, detail))),
            Task.FromResult(new Metadata()),
            () => new Status(code, detail),
            () => new Metadata(),
            () => { });

    /// <summary>
    /// Builds a mock DaprClient and a matching DaprPublishSubscribeGrpcClient. The capabilities
    /// mock returns true for any method so the version-aware bulk-publish wrapper uses the stable
    /// <c>BulkPublishEvent</c> RPC.
    /// </summary>
    private static (Mock<P.Dapr.DaprClient> Mock, DaprPublishSubscribeGrpcClient Client) CreateClient()
    {
        var mock = new Mock<P.Dapr.DaprClient>();
        var caps = new Mock<IDaprRuntimeCapabilities>();
        caps.Setup(c => c.SupportsMethodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var client = new DaprPublishSubscribeGrpcClient(
            mock.Object, new HttpClient(), new System.Text.Json.JsonSerializerOptions(), caps.Object);
        return (mock, client);
    }

    private static Mock<P.Dapr.DaprClient> SetupPublishEvent(Mock<P.Dapr.DaprClient> mock)
    {
        mock.Setup(c => c.PublishEventAsync(
                It.IsAny<P.PublishEventRequest>(),
                It.IsAny<CallOptions>()))
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));
        return mock;
    }

    // -----------------------------------------------------------------------
    //  PublishEventAsync<TData>
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PublishEventAsync_WithData_CallsGrpcPublishEvent()
    {
        var (mock, client) = CreateClient();
        SetupPublishEvent(mock);

        await client.PublishEventAsync(PubSubName, TopicName, new { Name = "order" }, TestContext.Current.CancellationToken);

        mock.Verify(c => c.PublishEventAsync(
            It.Is<P.PublishEventRequest>(r => r.PubsubName == PubSubName && r.Topic == TopicName),
            It.IsAny<CallOptions>()), Times.Once);
    }

    [Fact]
    public async Task PublishEventAsync_WithData_SerializesJsonAndSetsJsonContentType()
    {
        var (mock, client) = CreateClient();
        P.PublishEventRequest? captured = null;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        await client.PublishEventAsync(PubSubName, TopicName, new { Name = "order" }, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.True(captured!.Data.Length > 0);
        Assert.Equal(MessagingConstants.ContentTypeApplicationJson, captured.DataContentType);
    }

    [Fact]
    public async Task PublishEventAsync_WithCloudEvent_SetsCloudEventContentType()
    {
        var (mock, client) = CreateClient();
        P.PublishEventRequest? captured = null;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        var ce = new CloudEvent<TestOrder>(new TestOrder { Id = "42" })
        {
            Source = new Uri("urn:test"),
            Type = "test.order",
        };

        await client.PublishEventAsync(PubSubName, TopicName, ce, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(MessagingConstants.ContentTypeCloudEvent, captured!.DataContentType);
    }

    [Fact]
    public async Task PublishEventAsync_WithDataAndOptions_ForwardsMetadataAndOverrides()
    {
        var (mock, client) = CreateClient();
        P.PublishEventRequest? captured = null;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        var options = new PublishOptions
        {
            ContentType = "application/custom",
            Id = "evt-1",
            Source = new Uri("urn:app"),
            Type = "custom.type",
            Subject = "sub",
            TraceParent = "tp",
            TraceState = "ts",
            Metadata = { ["foo"] = "bar" },
        };

        await client.PublishEventAsync(PubSubName, TopicName, new { Name = "x" }, options, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("application/custom", captured!.DataContentType);
        Assert.Contains(captured.Metadata, kvp => kvp.Key == "foo" && kvp.Value == "bar");
        Assert.Contains(captured.Metadata, kvp => kvp.Key == "cloudevent.id" && kvp.Value == "evt-1");
        Assert.Contains(captured.Metadata, kvp => kvp.Key == "cloudevent.source" && kvp.Value == "urn:app");
        Assert.Contains(captured.Metadata, kvp => kvp.Key == "cloudevent.type" && kvp.Value == "custom.type");
        Assert.Contains(captured.Metadata, kvp => kvp.Key == "cloudevent.subject" && kvp.Value == "sub");
        Assert.Contains(captured.Metadata, kvp => kvp.Key == "cloudevent.traceparent" && kvp.Value == "tp");
        Assert.Contains(captured.Metadata, kvp => kvp.Key == "cloudevent.tracestate" && kvp.Value == "ts");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task PublishEventAsync_InvalidPubsub_Throws(string? bad)
    {
        var (_, client) = CreateClient();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.PublishEventAsync(bad!, TopicName, new { }, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task PublishEventAsync_InvalidTopic_Throws(string? bad)
    {
        var (_, client) = CreateClient();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.PublishEventAsync(PubSubName, bad!, new { }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishEventAsync_NullData_Throws()
    {
        var (_, client) = CreateClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PublishEventAsync<TestOrder>(PubSubName, TopicName, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishEventAsync_NullOptions_Throws()
    {
        var (_, client) = CreateClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PublishEventAsync(PubSubName, TopicName, new { }, (PublishOptions)null!, TestContext.Current.CancellationToken));
    }

    // -----------------------------------------------------------------------
    //  PublishEventAsync (no data)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PublishEventAsync_NoData_SendsEmptyEvent()
    {
        var (mock, client) = CreateClient();
        P.PublishEventRequest? captured = null;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        await client.PublishEventAsync(PubSubName, TopicName, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(PubSubName, captured!.PubsubName);
        Assert.Equal(TopicName, captured.Topic);
        Assert.True(captured.Data.IsEmpty);
    }

    // -----------------------------------------------------------------------
    //  PublishByteEventAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PublishByteEventAsync_SendsRawBytesWithContentType()
    {
        var (mock, client) = CreateClient();
        P.PublishEventRequest? captured = null;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        var payload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });
        await client.PublishByteEventAsync(PubSubName, TopicName, payload, "application/octet-stream",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("application/octet-stream", captured!.DataContentType);
        Assert.Equal(ByteString.CopyFrom(1, 2, 3), captured.Data);
    }

    [Fact]
    public async Task PublishByteEventAsync_DefaultContentType_IsJson()
    {
        var (mock, client) = CreateClient();
        P.PublishEventRequest? captured = null;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        await client.PublishByteEventAsync(PubSubName, TopicName, new byte[] { 9 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(MessagingConstants.ContentTypeApplicationJson, captured!.DataContentType);
    }

    [Fact]
    public async Task PublishByteEventAsync_WithOptionsContentType_OverridesDefault()
    {
        var (mock, client) = CreateClient();
        P.PublishEventRequest? captured = null;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        await client.PublishByteEventAsync(PubSubName, TopicName, new byte[] { 0 },
            "text/plain", new PublishOptions { ContentType = "application/custom" },
            TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("application/custom", captured!.DataContentType);
    }

    // -----------------------------------------------------------------------
    //  RpcException → DaprException
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PublishEventAsync_RpcError_ThrowsDaprException()
    {
        var (mock, client) = CreateClient();
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateFailingUnaryCall<Google.Protobuf.WellKnownTypes.Empty>(StatusCode.Internal, "boom"));

        var ex = await Assert.ThrowsAsync<DaprException>(() =>
            client.PublishEventAsync(PubSubName, TopicName, TestContext.Current.CancellationToken));
        Assert.Contains("Publish operation failed", ex.Message);
    }

    // -----------------------------------------------------------------------
    //  BulkPublishEventAsync
    // -----------------------------------------------------------------------

    private static Mock<P.Dapr.DaprClient> SetupBulkPublish(
        Mock<P.Dapr.DaprClient> mock,
        P.BulkPublishResponse response)
    {
        mock.Setup(c => c.BulkPublishEventAsync(
                It.IsAny<P.BulkPublishRequest>(),
                It.IsAny<CallOptions>()))
            .Returns(CreateUnaryCall(response));
        return mock;
    }

    [Fact]
    public async Task BulkPublishEventAsync_SerializesEventsAndAssignsEntryIds()
    {
        var (mock, client) = CreateClient();
        P.BulkPublishRequest? captured = null;
        mock.Setup(c => c.BulkPublishEventAsync(It.IsAny<P.BulkPublishRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.BulkPublishRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new P.BulkPublishResponse()));

        var events = new List<TestOrder>
        {
            new() { Id = "a" },
            new() { Id = "b" },
        };

        var response = await client.BulkPublishEventAsync(PubSubName, TopicName, events, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Entries.Count);
        Assert.Equal("0", captured.Entries[0].EntryId);
        Assert.Equal("1", captured.Entries[1].EntryId);
        Assert.True(captured.Entries[0].Event.Length > 0);
        Assert.Equal(MessagingConstants.ContentTypeApplicationJson, captured.Entries[0].ContentType);
        Assert.Empty(response.FailedEntries);
    }

    [Fact]
    public async Task BulkPublishEventAsync_WithCloudEvent_SetsCloudEventContentType()
    {
        var (mock, client) = CreateClient();
        P.BulkPublishRequest? captured = null;
        mock.Setup(c => c.BulkPublishEventAsync(It.IsAny<P.BulkPublishRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.BulkPublishRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new P.BulkPublishResponse()));

        var ce = new CloudEvent<TestOrder>(new TestOrder { Id = "x" });

        await client.BulkPublishEventAsync(PubSubName, TopicName, new object[] { ce }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(MessagingConstants.ContentTypeCloudEvent, captured!.Entries[0].ContentType);
    }

    [Fact]
    public async Task BulkPublishEventAsync_WithByteArrayEvents_SetsOctetStreamContentType()
    {
        var (mock, client) = CreateClient();
        P.BulkPublishRequest? captured = null;
        mock.Setup(c => c.BulkPublishEventAsync(It.IsAny<P.BulkPublishRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.BulkPublishRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new P.BulkPublishResponse()));

        var events = new List<byte[]> { new byte[] { 1 }, new byte[] { 2, 3 } };

        await client.BulkPublishEventAsync(PubSubName, TopicName, events, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Entries.Count);
        Assert.Equal(MessagingConstants.ContentTypeApplicationOctetStream, captured.Entries[0].ContentType);
        Assert.Equal(MessagingConstants.ContentTypeApplicationOctetStream, captured.Entries[1].ContentType);
    }

    [Fact]
    public async Task BulkPublishEventAsync_WithFailedEntries_ReturnsThemMapped()
    {
        var (mock, client) = CreateClient();
        var grpcResponse = new P.BulkPublishResponse();
        grpcResponse.FailedEntries.Add(new P.BulkPublishResponseFailedEntry { EntryId = "1", Error = "timeout" });
        SetupBulkPublish(mock, grpcResponse);

        var events = new List<TestOrder> { new() { Id = "a" }, new() { Id = "b" } };
        var response = await client.BulkPublishEventAsync(PubSubName, TopicName, events, cancellationToken: TestContext.Current.CancellationToken);

        var failed = Assert.Single(response.FailedEntries);
        Assert.Equal("timeout", failed.ErrorMessage);
        Assert.Equal("1", failed.Entry.EntryId);
        Assert.Equal("b", failed.Entry.EventData.Id);
    }

    [Fact]
    public async Task BulkPublishEventAsync_WithRequestMetadata_ForwardsToEnvelope()
    {
        var (mock, client) = CreateClient();
        P.BulkPublishRequest? captured = null;
        mock.Setup(c => c.BulkPublishEventAsync(It.IsAny<P.BulkPublishRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.BulkPublishRequest, CallOptions>((req, _) => captured = req)
            .Returns(CreateUnaryCall(new P.BulkPublishResponse()));

        var options = new PublishOptions { Metadata = { ["ttl"] = "60" } };
        await client.BulkPublishEventAsync(PubSubName, TopicName, new[] { new TestOrder { Id = "a" } },
            options, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Contains(captured!.Metadata, kvp => kvp.Key == "ttl" && kvp.Value == "60");
        Assert.Contains(captured.Entries[0].Metadata, kvp => kvp.Key == "ttl" && kvp.Value == "60");
    }

    [Fact]
    public async Task BulkPublishEventAsync_RpcError_ThrowsDaprException()
    {
        var (mock, client) = CreateClient();
        mock.Setup(c => c.BulkPublishEventAsync(It.IsAny<P.BulkPublishRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateFailingUnaryCall<P.BulkPublishResponse>(StatusCode.Unavailable, "down"));

        var ex = await Assert.ThrowsAsync<DaprException>(() =>
            client.BulkPublishEventAsync(PubSubName, TopicName, new[] { new TestOrder { Id = "a" } },
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("Bulk Publish operation failed", ex.Message);
    }

    [Fact]
    public async Task BulkPublishEventAsync_NullEvents_Throws()
    {
        var (_, client) = CreateClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.BulkPublishEventAsync<TestOrder>(PubSubName, TopicName, null!,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    // -----------------------------------------------------------------------
    //  API token header forwarding
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PublishEventAsync_WithApiToken_ForwardsDaprApiTokenHeader()
    {
        var mock = new Mock<P.Dapr.DaprClient>();
        var caps = new Mock<IDaprRuntimeCapabilities>();
        caps.Setup(c => c.SupportsMethodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var client = new DaprPublishSubscribeGrpcClient(
            mock.Object, new HttpClient(), new System.Text.Json.JsonSerializerOptions(), caps.Object, daprApiToken: "secret-token");

        CallOptions capturedOptions = default;
        mock.Setup(c => c.PublishEventAsync(It.IsAny<P.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<P.PublishEventRequest, CallOptions>((_, opts) => capturedOptions = opts)
            .Returns(CreateUnaryCall(new Google.Protobuf.WellKnownTypes.Empty()));

        await client.PublishEventAsync(PubSubName, TopicName, TestContext.Current.CancellationToken);

        Assert.Contains(capturedOptions.Headers!, e => e.Key == "dapr-api-token" && e.Value == "secret-token");
    }

    public sealed class TestOrder { public string Id { get; set; } = string.Empty; }
}
