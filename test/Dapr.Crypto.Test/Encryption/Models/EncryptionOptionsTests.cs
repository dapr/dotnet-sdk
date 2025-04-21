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

using Dapr.Crypto.Encryption.Models;

namespace Dapr.Crypto.Test.Encryption.Models;

public class EncryptionOptionsTests
{
    [Fact]
    public void Constructor_InitializesKeyWrapAlgorithm()
    {
        var keyWrapAlgorithm = KeyWrapAlgorithm.RsaOaep256;
        var options = new EncryptionOptions(keyWrapAlgorithm);
        Assert.Equal(keyWrapAlgorithm, options.KeyWrapAlgorithm);
    }

    [Fact]
    public void DefaultStreamingBlockSizeInBytes_Is4Kb()
    {
        var options = new EncryptionOptions(KeyWrapAlgorithm.A256kw);
        var defaultBlockSize = options.StreamingBlockSizeInBytes;
        Assert.Equal(4 * 1024, defaultBlockSize);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4092)]
    public void StreamingBlockSizeInBytes_SetValidValue_UpdatesBlockSize(int newBlockSize)
    {
        var options = new EncryptionOptions(KeyWrapAlgorithm.A192cbc);
        options.StreamingBlockSizeInBytes = newBlockSize;
        Assert.Equal(newBlockSize, options.StreamingBlockSizeInBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1024)]
    public void StreamingBlockSizeInBytes_SetInvalidValue_ThrowsArgumentOutOfRangeException(int invalidBlockSize)
    {
        var options = new EncryptionOptions(KeyWrapAlgorithm.Rsa);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.StreamingBlockSizeInBytes = invalidBlockSize);
    }

    [Fact]
    public void DefaultDecryptionKeyName_IsNull()
    {
        var options = new EncryptionOptions(KeyWrapAlgorithm.A256cbc);
        string? defaultKeyName = options.DecryptionKeyName;
        Assert.Null(defaultKeyName);
    }

    [Fact]
    public void DefaultEncryptionCipher_IsAes()
    {
        var options = new EncryptionOptions(KeyWrapAlgorithm.Aes);
        var defaultCipher = options.EncryptionCipher;
        Assert.Equal(DataEncryptionCipher.AesGcm, defaultCipher);
    }
}
