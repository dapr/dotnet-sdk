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
/// A collection of options used to configure how encryption cryptographic operations are performed.
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// Creates a new instance of the <see cref="EncryptionOptions"/>.
    /// </summary>
    /// <param name="keyWrapAlgorithm"></param>
    public EncryptionOptions(KeyWrapAlgorithm keyWrapAlgorithm)
    {
        KeyWrapAlgorithm = keyWrapAlgorithm;
    }

    /// <summary>
    /// The name of the algorithm used to wrap the encryption key.
    /// </summary>
    public KeyWrapAlgorithm KeyWrapAlgorithm { get; set; }

    private int streamingBlockSizeInBytes = 4 * 1024; // 4 KB
    /// <summary>
    /// The size of the block in bytes used to send data to the sidecar for cryptography operations.
    /// </summary>
    /// <remarks>
    /// This defaults to 4KB and generally should not exceed 64KB.
    /// </remarks>
    public int StreamingBlockSizeInBytes
    {
        get => streamingBlockSizeInBytes;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
                
            streamingBlockSizeInBytes = value;
        }
    }
        
    /// <summary>
    /// The optional name (and optionally a version) of the key specified to use during decryption.
    /// </summary>
    public string? DecryptionKeyName { get; set; } = null;

    /// <summary>
    /// The name of the cipher to use for the encryption operation.
    /// </summary>
    public DataEncryptionCipher EncryptionCipher { get; set; } = DataEncryptionCipher.AesGcm;
}
