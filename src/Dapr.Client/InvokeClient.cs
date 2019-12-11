// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A client for interacting with the Dapr invoke endpoints.
    /// </summary>
    public abstract class InvokeClient
    {
        /// <summary>
        /// Invokes a method using the Dapr invoke endpoints using the properties supplied in the  <paramref name="envelope" /> variable.
        /// </summary>
        /// <param name="envelope">The envelope containing the invoke request parameters.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request.</returns>
        public abstract Task<TValue> InvokeMethodAsync<TValue>(InvokeEnvelope envelope, CancellationToken cancellationToken = default);
    }
}