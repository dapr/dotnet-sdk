// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Runtime.Serialization;

namespace Dapr.Client;

/// <summary>
/// The cipher used for data encryption operations.
/// </summary>
public enum DataEncryptionCipher
{
    /// <summary>
    /// The default data encryption cipher used, this represents AES GCM.
    /// </summary>
    [EnumMember(Value = "aes-gcm")]
    AesGcm,
    /// <summary>
    /// Represents the ChaCha20-Poly1305 data encryption cipher.
    /// </summary>
    [EnumMember(Value = "chacha20-poly1305")]
    ChaCha20Poly1305
};

/// <summary>
/// The algorithm used for key wrapping cryptographic operations.
/// </summary>
public enum KeyWrapAlgorithm
{
    /// <summary>
    /// Represents the AES key wrap algorithm.
    /// </summary>
    [EnumMember(Value="A256KW")]
    Aes,
    /// <summary>
    /// An alias for the AES key wrap algorithm.
    /// </summary>
    [EnumMember(Value="A256KW")]
    A256kw,
    /// <summary>
    /// Represents the AES 128 CBC key wrap algorithm.
    /// </summary>
    [EnumMember(Value="A128CBC")]
    A128cbc,
    /// <summary>
    /// Represents the AES 192 CBC key wrap algorithm.
    /// </summary>
    [EnumMember(Value="A192CBC")]
    A192cbc,
    /// <summary>
    /// Represents the AES 256 CBC key wrap algorithm.
    /// </summary>
    [EnumMember(Value="A256CBC")]
    A256cbc,
    /// <summary>
    /// Represents the RSA key wrap algorithm.
    /// </summary>
    [EnumMember(Value= "RSA-OAEP-256")]
    Rsa,
    /// <summary>
    /// An alias for the RSA key wrap algorithm.
    /// </summary>
    [EnumMember(Value= "RSA-OAEP-256")]
    RsaOaep256 //Alias for RSA
}