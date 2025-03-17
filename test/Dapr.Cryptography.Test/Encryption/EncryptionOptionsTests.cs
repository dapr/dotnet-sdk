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

using System;
using Dapr.Cryptography.Encryption;

namespace Dapr.Cryptography.Test.Encryption;

public class EncryptionOptionsTests
{
    [Fact]
    public void Constructor_ShouldInitializeKeyWrapAlgorithm()
    {
        // Arrange
        const KeyWrapAlgorithm keyWrapAlgorithm = KeyWrapAlgorithm.RsaOaep256;

        // Act
        var options = new EncryptionOptions(keyWrapAlgorithm);

        // Assert
        Assert.Equal(keyWrapAlgorithm, options.KeyWrapAlgorithm);
    }

    [Fact]
    public void StreamingBlockSizeInBytes_ShouldReturnDefaultValue()
    {
        // Arrange
        var options = new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256);

        // Act
        var blockSize = options.StreamingBlockSizeInBytes;

        // Assert
        Assert.Equal(4 * 1024, blockSize); // Default value is 4KB
    }

    [Fact]
    public void StreamingBlockSizeInBytes_ShouldSetValidValue()
    {
        // Arrange
        var options = new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256);
        const int newBlockSize = 8 * 1024; // 8KB

        // Act
        options.StreamingBlockSizeInBytes = newBlockSize;
        var blockSize = options.StreamingBlockSizeInBytes;

        // Assert
        Assert.Equal(newBlockSize, blockSize);
    }

    [Fact]
    public void StreamingBlockSizeInBytes_ShouldThrowArgumentOutOfRangeException_ForInvalidValue()
    {
        // Arrange
        var options = new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.StreamingBlockSizeInBytes = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.StreamingBlockSizeInBytes = -1);
    }

    [Fact]
    public void DecryptionKeyName_ShouldBeNullByDefault()
    {
        // Arrange
        var options = new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256);

        // Act
        var decryptionKeyName = options.DecryptionKeyName;

        // Assert
        Assert.Null(decryptionKeyName);
    }

    [Fact]
    public void EncryptionCipher_ShouldReturnDefaultValue()
    {
        // Arrange
        var options = new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256);

        // Act
        var encryptionCipher = options.EncryptionCipher;

        // Assert
        Assert.Equal(DataEncryptionCipher.AesGcm, encryptionCipher); // Default value is AesGcm
    }
}
