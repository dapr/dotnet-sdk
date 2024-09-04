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
/// Maintains access to 
/// </summary>
internal sealed class ConnectionManager : IAsyncDisposable
{
    /// <summary>
    /// A reference to the DaprClient instance.
    /// </summary>
    private readonly P.Dapr.DaprClient _client;
    /// <summary>
    /// Used to ensure thread-safe operations against the stream.
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    /// <summary>
    /// The stream connection between this instance and the Dapr sidecar.
    /// </summary>
    private AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest>?
        _stream;

    public ConnectionManager(P.Dapr.DaprClient client)
    {
        _client = client;
    }

    public async
        Task<AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest>>
        GetStreamAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            return _stream ??= _client.SubscribeTopicEventsAlpha1(cancellationToken: cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.RequestStream.CompleteAsync();
        }

        _semaphore.Dispose();
    }
}
