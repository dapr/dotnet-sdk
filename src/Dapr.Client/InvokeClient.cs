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
        /// Invokes the specified method on target service, Request data is serialized using the default System.Text.Json Serialization.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="data">The data to be sent within the body of the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task InvokeMethodAsync<TRequest>(string serviceName, string methodName, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes the specified method on target service, Response data is deserialized using the default System.Text.Json Serialization.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TResponse">The data type that the Dapr response body will be deserialized to.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request. If the Dapr response content is null the return type will be null.</returns>
        public abstract Task<TResponse> InvokeMethodAsync<TResponse>(string serviceName, string methodName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes the specified method on target service, Request and Response data is serialized/deserialized using the default System.Text.Json Serialization.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="data">The data to be sent within the body of the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
        /// <typeparam name="TResponse">The data type that the Dapr response body will be deserialized to.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request. If the Dapr response content is null the return type will be null.</returns>
        public abstract Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string serviceName, string methodName, TRequest data, CancellationToken cancellationToken = default);
    }
}