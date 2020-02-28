// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Client
{
    /// <summary>
    /// A client for interacting with the Dapr endpoints.
    /// </summary>
    public class DaprClient : IDaprClient
    {
        /// <inheritdoc/>
        public Task InvokeMethodAsync(string serviceName, string methodName, IReadOnlyDictionary<string, string> metadata = null, CancellationToken cancellationToken = default)
        {
            
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Task InvokeMethodAsync<TRequest>(string serviceName, string methodName, TRequest data, IReadOnlyDictionary<string, string> metadata = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<TResponse> InvokeMethodAsync<TResponse>(string serviceName, string methodName, IReadOnlyDictionary<string, string> metadata = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string serviceName, string methodName, TRequest data, IReadOnlyDictionary<string, string> metadata = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Task PublishEventAsync<TRequest>(string topicName, TRequest publishContent, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Task PublishEventAsync(string topicName, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
