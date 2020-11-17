// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using Dapr.Client;
    using Dapr.Client.Http;

    /// <summary>
    /// Represents an Invoke Request used for service invocation
    /// </summary>
    /// <typeparam name="TRequest">Data type of the request.</typeparam>
    public sealed class InvocationRequest<TRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvocationRequest{TRequest}"/> class.
        /// </summary>
        public InvocationRequest()
        {
        }

        /// <summary>
        /// Gets or sets the app identifier.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Gets or sets the method name to invoke.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        public TRequest Body { get; set; }

        /// <summary>
        /// Gets or sets the HTTP extension info.
        /// </summary>
        public HTTPExtension HttpExtension { get; set; }
    }
}
