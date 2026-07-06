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
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Dapr.Cryptography.Encryption.Models;

namespace Dapr.Cryptography.Test.Encryption.Models;

public class DataEncryptionCipherTests
{
    [Theory]
    [InlineData(DataEncryptionCipher.AesGcm, "aes-gcm")]
    [InlineData(DataEncryptionCipher.ChaCha20Poly1305, "chacha20-poly1305")]
    public void EnumMemberValue_IsCorrect(DataEncryptionCipher cipher, string expectedValue)
    {
        var memberInfo = typeof(DataEncryptionCipher).GetMember(cipher.ToString()).First();
        var attribute = memberInfo.GetCustomAttribute<EnumMemberAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedValue, attribute.Value);
    }

    [Fact]
    public void AllValues_HaveEnumMemberAttribute()
    {
        foreach (var value in Enum.GetValues<DataEncryptionCipher>())
        {
            var memberInfo = typeof(DataEncryptionCipher).GetMember(value.ToString()).First();
            var attribute = memberInfo.GetCustomAttribute<EnumMemberAttribute>();
            Assert.NotNull(attribute);
        }
    }

    [Fact]
    public void AesGcm_IsDefaultValue()
    {
        Assert.Equal(0, (int)DataEncryptionCipher.AesGcm);
    }
}
