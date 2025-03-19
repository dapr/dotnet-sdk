// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Grpc.Core;

namespace Dapr.Cryptography.Encryption;

/// <summary>
/// Base class for process streams of data for both encryption and decryption operations.
/// </summary>
/// <typeparam name="TRequest">The type of request message presented to the Dapr sidecar for the operation.</typeparam>
/// <typeparam name="TRequestOptions">The type of request message options presented to the Dapr sidecar to configure the operation.</typeparam>
/// <typeparam name="TResponse">The type of response message provided by the Dapr sidecar for the operation.</typeparam>
internal abstract class DaprEncryptionPipelineProcessorBase<TRequest, TRequestOptions, TResponse>
{
    /// <summary>
    /// Sends the stream from the SDK to the Dapr sidecar.
    /// </summary>
    /// <param name="stream">The stream containing the data to be processed.</param>
    /// <param name="blockSize">The size of the blocks to be read from the stream.</param>
    /// <param name="duplexStream">The duplex stream used for sending and receiving data.</param>
    /// <param name="options">The options for the request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected abstract Task SendStreamAsync(
        Stream stream,
        int blockSize,
        AsyncDuplexStreamingCall<TRequest, TResponse> duplexStream,
        TRequestOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the processed stream data from the Dapr sidecar.
    /// </summary>
    /// <param name="duplexStream">The duplex stream used for receiving data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous enumerable of the processed data.</returns>
    protected abstract IAsyncEnumerable<ReadOnlyMemory<byte>> RetrieveStreamAsync(
        AsyncDuplexStreamingCall<TRequest, TResponse> duplexStream,
        CancellationToken cancellationToken);

    /// <summary>
    /// Processes the stream by reading from the input stream and writing to the duplex stream.
    /// </summary>
    /// <param name="stream">The stream containing the data to be processed.</param>
    /// <param name="blockSize">The size of the blocks to be read from the stream.</param>
    /// <param name="duplexStream">The duplex stream used for sending and receiving data.</param>
    /// <param name="request">The request to the sidecar.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous enumerable of the processed data.</returns>
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ProcessStreamAsync
        (Stream stream,
            int blockSize,
            AsyncDuplexStreamingCall<TRequest, TResponse> duplexStream,
            TRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pipe = new Pipe();
        var writer = FillPipeAsync(pipe.Writer, stream, blockSize, request, duplexStream, cancellationToken);
        var reading = RetrieveStreamAsync(duplexStream, cancellationToken);

        var writingTask = Task.Run(() => writer, cancellationToken);

        await foreach (var data in reading)
        {
            yield return data;
        }

        await writingTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Fills the pipe by reading from the input stream and writing to the pipe.
    /// </summary>
    /// <param name="writer">The pipe writer to write the data to.</param>
    /// <param name="stream">The stream containing the source data to be processed.</param>
    /// <param name="blockSize">The size of the blocks to be read from the input stream.</param>
    /// <param name="request">The input request.</param>
    /// <param name="duplexStream">The duplex stream used for sending encryption data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    private static async Task FillPipeAsync(
        PipeWriter writer,
        Stream stream,
        int blockSize,
        TRequest request,
        AsyncDuplexStreamingCall<TRequest, TResponse> duplexStream,
        CancellationToken cancellationToken)
    {
        await duplexStream.RequestStream.WriteAsync(request, cancellationToken);

        while (true)
        {
            var memory = writer.GetMemory(blockSize);
            var bytesRead = await stream.ReadAsync(memory, cancellationToken);
            if (bytesRead == 0)
                break;

            writer.Advance(bytesRead);

            var result = await writer.FlushAsync(cancellationToken);
            if (result.IsCompleted)
                break;
        }

        await writer.CompleteAsync();
        await duplexStream.RequestStream.CompleteAsync();
    }
}
