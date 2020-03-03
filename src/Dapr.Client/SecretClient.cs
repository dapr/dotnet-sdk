// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A client for interacting with the Dapr secret store.
    /// </summary>
    public abstract class SecretClient
    {
        /// <summary>
        /// Gets the current value associated with the <paramref name="secretName" /> from the Dapr secret store.
        /// </summary>
        /// <param name="storeName">The secret store name.</param>
        /// <param name="secretName">The secret name.</param>
        /// <param name="metadata">The secret metadata.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will return the value when the operation has completed.</returns>
        public abstract ValueTask<Dictionary<string, string>> GetSecretAsync(string storeName, string secretName, Dictionary<string, string> metadata = default(Dictionary<string, string>), CancellationToken cancellationToken = default);

    }
}
