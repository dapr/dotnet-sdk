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
        /// Invokes a method using the Dapr invoke endpoints.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="data">The data to be sent within the body of the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task InvokeMethodAsync(string serviceName, string methodName, string data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method using the Dapr invoke endpoints.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request. If the Dapr response content is null the return type will be null.</returns>
        public abstract Task<TValue> InvokeMethodAsync<TValue>(string serviceName, string methodName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method using the Dapr invoke endpoints.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="data">The data to be sent within the body of the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request. If the Dapr response content is null the return type will be null.</returns>
        public abstract Task<TValue> InvokeMethodAsync<TValue>(string serviceName, string methodName, string data, CancellationToken cancellationToken = default);
    }
}