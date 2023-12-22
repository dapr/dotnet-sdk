using System.Runtime.Serialization;

namespace Dapr.Client
{
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
        [EnumMember(Value="AES")]
        Aes,
        /// <summary>
        /// An alias for the AES key wrap algorithm.
        /// </summary>
        [EnumMember(Value="AES")]
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
        [EnumMember(Value="RSA")]
        Rsa,
        /// <summary>
        /// An alias for the RSA key wrap algorithm.
        /// </summary>
        [EnumMember(Value="RSA")]
        RsaOaep256 //Alias for RSA
    }
}
