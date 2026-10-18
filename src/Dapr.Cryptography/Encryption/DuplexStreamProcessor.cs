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

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;

namespace Dapr.Cryptography.Encryption;

internal abstract class DuplexStreamProcessor<TRequest, TResponse> : IDisposable, IAsyncDisposable
{
    private readonly Channel<ReadOnlyMemory<byte>> outputChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private CancellationTokenSource? processingCancellation;
    private CancellationTokenRegistration callCancellation;
    private Task processingTask = Task.CompletedTask;
    private bool disposed;

    protected Task StartProcessing(
        Stream inputStream,
        AsyncDuplexStreamingCall<TRequest, TResponse> call,
        int blockSize,
        Func<ByteString, ulong, TRequest> createRequest,
        Func<TResponse, ReadOnlyMemory<byte>> getResponseData,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (processingCancellation is not null)
        {
            throw new InvalidOperationException("The stream processor has already been started.");
        }

        processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = processingCancellation.Token;
        // Disposing the call also interrupts CompleteAsync, which has no cancellation token.
        callCancellation = token.Register(call.Dispose);

        var sending = RunAsync(async () =>
        {
            await using var bufferedStream = new BufferedStream(inputStream, blockSize);
            var buffer = new byte[blockSize];
            ulong sequenceNumber = 0;
            int bytesRead;
            while ((bytesRead = await bufferedStream.ReadAsync(buffer, token)) > 0)
            {
                await call.RequestStream.WriteAsync(
                    createRequest(ByteString.CopyFrom(buffer, 0, bytesRead), sequenceNumber++), token);
            }

            await call.RequestStream.CompleteAsync();
        });

        var receiving = RunAsync(async () =>
        {
            await foreach (var response in call.ResponseStream.ReadAllAsync(token))
            {
                await outputChannel.Writer.WriteAsync(getResponseData(response), token);
            }
        });

        processingTask = CompleteAsync(sending, receiving);
        return Task.CompletedTask;
    }

    private async Task RunAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            // Publish the original failure before cancellation can fail the other half of the call.
            outputChannel.Writer.TryComplete(exception);
            processingCancellation!.Cancel();
        }
    }

    private async Task CompleteAsync(Task sending, Task receiving)
    {
        await Task.WhenAll(sending, receiving);
        outputChannel.Writer.TryComplete();
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> GetProcessedDataAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var data in outputChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return data;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        processingCancellation?.Cancel();
        outputChannel.Writer.TryComplete();
        if (processingTask.IsCompleted)
        {
            DisposeCancellation();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        try
        {
            await processingTask;
        }
        finally
        {
            DisposeCancellation();
        }
    }

    private void DisposeCancellation()
    {
        callCancellation.Dispose();
        processingCancellation?.Dispose();
    }
}
