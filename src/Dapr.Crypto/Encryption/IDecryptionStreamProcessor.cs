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

using System.Runtime.CompilerServices;
using Grpc.Core;

namespace Dapr.Crypto.Encryption;

internal interface IDecryptionStreamProcessor
{
    /// <summary>
    /// Sends the provided bytes in chunks to the sidecar for the encryption operation.
    /// </summary>
    /// <param name="inputStream">The stream containing the bytes to decrypt.</param>
    /// <param name="call">The call to make to the sidecar to process the encryption operation.</param>
    /// <param name="streamingBlockSizeInBytes">The size, in bytes, of the streaming blocks.</param>
    /// <param name="options">The decryption options.</param>
    /// <param name="cancellationToken">Token used to cancel the ongoing request.</param>
    Task ProcessStreamAsync(
        Stream inputStream,
        AsyncDuplexStreamingCall<Client.Autogen.Grpc.v1.DecryptRequest, Client.Autogen.Grpc.v1.DecryptResponse> call,
        int streamingBlockSizeInBytes,
        Client.Autogen.Grpc.v1.DecryptRequestOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the processed bytes from the operation from the sidecar and
    /// returns as an enumerable stream.
    /// </summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> GetProcessedDataAsync(CancellationToken cancellationToken);
}
