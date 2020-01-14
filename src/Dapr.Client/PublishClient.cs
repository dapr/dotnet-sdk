// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A client for interacting with the Dapr publish endpoints.
    /// </summary>
    public abstract class PublishClient
    {
        /// <summary>
        /// Publish a new event to the specified topic.
        /// </summary>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="publishContent">The contents of the event.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task PublishEventAsync<TRequest>(string topicName, TRequest publishContent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish a new event to the specified topic.
        /// </summary>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" />.</returns>
        public abstract Task PublishEventAsync(string topicName, CancellationToken cancellationToken = default);
    }
}