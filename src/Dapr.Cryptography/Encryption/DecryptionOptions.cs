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

namespace Dapr.Cryptography.Encryption;

/// <summary>
/// A collection of options used to configure how decryption cryptographic operations are performed. 
/// </summary>
public sealed class DecryptionOptions
{
    private int streamingBlockSizeInBytes = 4 * 1024; // 4KB

    /// <summary>
    /// The size of the block in bytes used to send data to the sidecar for cryptography operations.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if a number less than or equal to zero
    /// is used for the block size.</exception>
    public int StreamingBlockSizeInBytes
    {
        get => streamingBlockSizeInBytes;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            streamingBlockSizeInBytes = value;
        }
    }
}
