﻿// ------------------------------------------------------------------------
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

using Dapr.Common;
using Dapr.Crypto.Encryption.Models;

namespace Dapr.Crypto.Encryption;

/// <summary>
/// Provides the implementation shape for the Dapr encryption client.
/// </summary>
public interface IDaprEncryptionClient : IDaprClient
{
    /// <summary>
    /// Encrypts an array of bytes using the Dapr Cryptography encryption functionality.
    /// </summary>
    /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
    /// <param name="plaintextBytes">The bytes of the plaintext value to encrypt.</param>
    /// <param name="keyName">The name of the key to use from the Vault for the encryption operation.</param>
    /// <param name="encryptionOptions">Options informing how the encryption operation should be configured.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>An array of encrypted bytes.</returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public Task<ReadOnlyMemory<byte>> EncryptAsync(string vaultResourceName,
        ReadOnlyMemory<byte> plaintextBytes, string keyName, EncryptionOptions encryptionOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts a stream using the Dapr Cryptography encryption functionality.
    /// </summary>
    /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
    /// <param name="plaintextStream">The stream containing the bytes of the plaintext value to encrypt.</param>
    /// <param name="keyName">The name of the key to use from the Vault for the encryption operation.</param>
    /// <param name="encryptionOptions">Options informing how the encryption operation should be configured.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>An array of encrypted bytes.</returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public Task<IAsyncEnumerable<ReadOnlyMemory<byte>>> EncryptAsync(string vaultResourceName, Stream plaintextStream, string keyName,
        EncryptionOptions encryptionOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts the specified ciphertext bytes using the Dapr Cryptography encryption functionality.
    /// </summary>
    /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
    /// <param name="ciphertextBytes">The bytes of the ciphertext value to decrypt.</param>
    /// <param name="keyName">The name of the key to use from the Vault for the decryption operation.</param>
    /// <param name="options">Options informing how the decryption operation should be configured.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>An array of decrypted bytes.</returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public Task<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, ReadOnlyMemory<byte> ciphertextBytes, string keyName, DecryptionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts the specified stream of ciphertext using the Dapr Cryptography encryption functionality.
    /// </summary>
    /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
    /// <param name="ciphertextStream">The stream containing the bytes of the ciphertext value to decrypt.</param>
    /// <param name="keyName">The name of the key to use from the Vault for the decryption operation.</param>
    /// <param name="options">Options informing how the decryption operation should be configured.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>An asynchronously enumerable array of decrypted bytes.</returns>
    [Obsolete(
        "The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public Task<IAsyncEnumerable<ReadOnlyMemory<byte>>> DecryptAsync(string vaultResourceName, Stream ciphertextStream,
        string keyName, DecryptionOptions? options = null, CancellationToken cancellationToken = default);
}
