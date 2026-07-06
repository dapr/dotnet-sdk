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

public class KeyWrapAlgorithmTests
{
    [Theory]
    [InlineData(KeyWrapAlgorithm.Aes, "A256KW")]
    [InlineData(KeyWrapAlgorithm.A256kw, "A256KW")]
    [InlineData(KeyWrapAlgorithm.A128cbc, "A128CBC")]
    [InlineData(KeyWrapAlgorithm.A192cbc, "A192CBC")]
    [InlineData(KeyWrapAlgorithm.A256cbc, "A256CBC")]
    [InlineData(KeyWrapAlgorithm.Rsa, "RSA-OAEP-256")]
    [InlineData(KeyWrapAlgorithm.RsaOaep256, "RSA-OAEP-256")]
    public void EnumMemberValue_IsCorrect(KeyWrapAlgorithm algorithm, string expectedValue)
    {
        var memberInfo = typeof(KeyWrapAlgorithm).GetMember(algorithm.ToString()).First();
        var attribute = memberInfo.GetCustomAttribute<EnumMemberAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedValue, attribute.Value);
    }

    [Fact]
    public void AllValues_HaveEnumMemberAttribute()
    {
        foreach (var value in Enum.GetValues<KeyWrapAlgorithm>())
        {
            var memberInfo = typeof(KeyWrapAlgorithm).GetMember(value.ToString()).First();
            var attribute = memberInfo.GetCustomAttribute<EnumMemberAttribute>();
            Assert.NotNull(attribute);
        }
    }

    [Fact]
    public void Aes_And_A256kw_HaveSameEnumMemberValue()
    {
        var aesMember = typeof(KeyWrapAlgorithm).GetMember(KeyWrapAlgorithm.Aes.ToString()).First();
        var a256kwMember = typeof(KeyWrapAlgorithm).GetMember(KeyWrapAlgorithm.A256kw.ToString()).First();

        var aesAttr = aesMember.GetCustomAttribute<EnumMemberAttribute>();
        var a256kwAttr = a256kwMember.GetCustomAttribute<EnumMemberAttribute>();

        Assert.Equal(aesAttr!.Value, a256kwAttr!.Value);
    }

    [Fact]
    public void Rsa_And_RsaOaep256_HaveSameEnumMemberValue()
    {
        var rsaMember = typeof(KeyWrapAlgorithm).GetMember(KeyWrapAlgorithm.Rsa.ToString()).First();
        var rsaOaepMember = typeof(KeyWrapAlgorithm).GetMember(KeyWrapAlgorithm.RsaOaep256.ToString()).First();

        var rsaAttr = rsaMember.GetCustomAttribute<EnumMemberAttribute>();
        var rsaOaepAttr = rsaOaepMember.GetCustomAttribute<EnumMemberAttribute>();

        Assert.Equal(rsaAttr!.Value, rsaOaepAttr!.Value);
    }
}
