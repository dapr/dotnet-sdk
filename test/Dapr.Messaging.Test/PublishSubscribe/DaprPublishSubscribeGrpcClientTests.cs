// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Dapr.Messaging.PublishSubscribe;
using Grpc.Core;
using Moq;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.Test.PublishSubscribe;

public class DaprPublishSubscribeGrpcClientTests
{
    private const string PubSubName = "testPubSub";
    private const string TopicName = "testTopic";

    private static DaprSubscriptionOptions DefaultOptions() =>
        new(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        {
            MaximumQueuedMessages = 100,
            MaximumCleanupTimeout = TimeSpan.FromSeconds(1)
        };

    private static TopicMessageHandler SuccessHandler() =>
        (_, _) => Task.FromResult(TopicResponseAction.Success);

    /// <summary>
    /// Builds a mock DaprClient pre-wired with an AsyncDuplexStreamingCall that immediately signals
    /// end-of-stream on the response side, and returns the mock for verification.
    /// </summary>
    private static Mock<P.Dapr.DaprClient> BuildMockDaprClient()
    {
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // End-of-stream immediately so background tasks complete cleanly.
        mockResponseStream
            .Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockCall =
            new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
                mockRequestStream.Object, mockResponseStream.Object,
                Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient
            .Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        return mockDaprClient;
    }

    private static DaprPublishSubscribeGrpcClient CreateGrpcClient(Mock<P.Dapr.DaprClient> mockDaprClient,
        string? apiToken = null) =>
        new(mockDaprClient.Object, new HttpClient(), Mock.Of<Dapr.Common.IDaprRuntimeCapabilities>(), apiToken);

    // -------------------------------------------------------------------------
    // SubscribeAsync tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// SubscribeAsync must return a non-null IAsyncDisposable on the happy path.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_ReturnsNonNullDisposable()
    {
        var mockDaprClient = BuildMockDaprClient();
        var client = CreateGrpcClient(mockDaprClient);

        var disposable = await client.SubscribeAsync(PubSubName, TopicName, DefaultOptions(), SuccessHandler(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(disposable);
        await disposable.DisposeAsync();
    }

    /// <summary>
    /// SubscribeAsync must return a PublishSubscribeReceiver (the concrete IAsyncDisposable produced by the client).
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_ReturnedDisposable_IsPublishSubscribeReceiver()
    {
        var mockDaprClient = BuildMockDaprClient();
        var client = CreateGrpcClient(mockDaprClient);

        var disposable = await client.SubscribeAsync(PubSubName, TopicName, DefaultOptions(), SuccessHandler(),
            TestContext.Current.CancellationToken);

        Assert.IsType<PublishSubscribeReceiver>(disposable);
        await disposable.DisposeAsync();
    }

    /// <summary>
    /// SubscribeAsync must initiate the gRPC duplex stream with the Dapr sidecar exactly once.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_InitiatesGrpcStreamExactlyOnce()
    {
        var mockDaprClient = BuildMockDaprClient();
        var client = CreateGrpcClient(mockDaprClient);

        var disposable = await client.SubscribeAsync(PubSubName, TopicName, DefaultOptions(), SuccessHandler(),
            TestContext.Current.CancellationToken);

        mockDaprClient.Verify(
            c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()),
            Times.Once);

        await disposable.DisposeAsync();
    }

    /// <summary>
    /// The IAsyncDisposable returned by SubscribeAsync must complete both its internal channels when disposed,
    /// confirming that the returned receiver is fully functional.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_ReturnedReceiver_ChannelsCompleteAfterDispose()
    {
        var mockDaprClient = BuildMockDaprClient();
        var client = CreateGrpcClient(mockDaprClient);

        var disposable = await client.SubscribeAsync(PubSubName, TopicName, DefaultOptions(), SuccessHandler(),
            TestContext.Current.CancellationToken);

        var receiver = Assert.IsType<PublishSubscribeReceiver>(disposable);
        await disposable.DisposeAsync();

        Assert.True(receiver.TopicMessagesChannelCompletion.IsCompleted);
        Assert.True(receiver.AcknowledgementsChannelCompletion.IsCompleted);
    }

    /// <summary>
    /// When a pre-cancelled CancellationToken is supplied, SubscribeAsync must propagate the
    /// OperationCanceledException thrown while attempting to acquire the gRPC stream.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WithPreCancelledToken_ThrowsOperationCanceledException()
    {
        var mockDaprClient = BuildMockDaprClient();
        var client = CreateGrpcClient(mockDaprClient);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SubscribeAsync(PubSubName, TopicName, DefaultOptions(), SuccessHandler(), cts.Token));
    }

    // -------------------------------------------------------------------------
    // Dispose tests (DaprPublishSubscribeClient / DaprPublishSubscribeGrpcClient)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calling Dispose() on a DaprPublishSubscribeGrpcClient must dispose the underlying HttpClient.
    /// After disposal any attempt to use the HttpClient throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public void Dispose_DisposesUnderlyingHttpClient()
    {
        var httpClient = new HttpClient();
        var client = new DaprPublishSubscribeGrpcClient(
            BuildMockDaprClient().Object, httpClient, Mock.Of<Dapr.Common.IDaprRuntimeCapabilities>(), daprApiToken: null);

        client.Dispose();

        // The HttpClient stored on the base class is the exact instance we passed in.
        Assert.ThrowsAny<ObjectDisposedException>(() => httpClient.Send(new HttpRequestMessage(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The base-class Dispose() guard must prevent double-disposal: calling Dispose() a second time
    /// must not throw even though the underlying HttpClient is already disposed.
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var client = CreateGrpcClient(BuildMockDaprClient());

        client.Dispose();

        // Second call must be a silent no-op.
        var ex = Record.Exception(() => client.Dispose());
        Assert.Null(ex);
    }
}
