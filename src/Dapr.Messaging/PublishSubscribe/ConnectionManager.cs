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

using Grpc.Core;
using C = Dapr.AppCallback.Autogen.Grpc.v1;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// Maintains the streaming connection to the Dapr sidecar so it can be repurposed without
/// multiple callers opening separate connections.
/// </summary>
internal sealed class ConnectionManager : IAsyncDisposable
{
    /// <summary>
    /// A reference to the DaprClient instance.
    /// </summary>
    private readonly P.Dapr.DaprClient client;
    /// <summary>
    /// Used to ensure thread-safe operations against the stream.
    /// </summary>
    private readonly SemaphoreSlim semaphore = new(1, 1);
    /// <summary>
    /// The stream connection between this instance and the Dapr sidecar.
    /// </summary>
    private AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest>?
        stream;

    public ConnectionManager(P.Dapr.DaprClient client)
    {
        this.client = client;
    }

    /// <summary>
    /// Retrieves or creates the bidirectional stream to the DaprClient for transacting pub/sub subscriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task<AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest>> GetStreamAsync(CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            return stream ??= client.SubscribeTopicEventsAlpha1(cancellationToken: cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (stream is not null)
        {
            await stream.RequestStream.CompleteAsync();
        }

        semaphore.Dispose();
    }
}
