#nullable enable
namespace Dapr.Client
{
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

        /// <summary>
        /// The size of the block in bytes used to send data to the sidecar for cryptography operations.
        /// </summary>
        /// <remarks>
        /// This defaults to 4KB and generally should not exceed 64KB.
        /// </remarks>
        public uint StreamingBlockSizeInBytes { get; set; } = 4 * 1024;

        /// <summary>
        /// The optional name (and optionally a version) of the key specified to use during decryption.
        /// </summary>
        public string? DecryptionKeyName { get; set; } = null;

        /// <summary>
        /// The name of the cipher to use for the encryption operation.
        /// </summary>
        public DataEncryptionCipher EncryptionCipher { get; set; } = DataEncryptionCipher.AesGcm;
    }

    /// <summary>
    /// A collection fo options used to configure how decryption cryptographic operations are performed.
    /// </summary>
    public class DecryptionOptions
    {
        /// <summary>
        /// The size of the block in bytes used to send data to the sidecar for cryptography operations.
        /// </summary>
        public uint StreamingBlockSizeInBytes { get; set; } = 4 * 1024;
    }
}
