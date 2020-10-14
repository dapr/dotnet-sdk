// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using Dapr.Client;
    using Dapr.Client.Http;

    /// <summary>
    /// Represents an Invoke Request used for service invocation
    /// </summary>
    public sealed class InvokeRequest
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeRequest"/> class.
        /// </summary>
        /// <param name="appId">The app identifier.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="body">The request body.</param>
        /// <param name="contentType">The content type.</param>
        /// <param name="httpExtension">HTTP extension info (optional).</param>
        public InvokeRequest(string appId, string methodName, byte[] body, string contentType, HTTPExtension httpExtension)
        {
            ArgumentVerifier.ThrowIfNull(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNull(methodName, nameof(methodName));

            this.AppId = appId;
            this.MethodName = methodName;
            this.Body = body;
            this.ContentType = contentType;
            this.HttpExtension = httpExtension;
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
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the content type.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the HTTP extension info.
        /// </summary>
        public HTTPExtension HttpExtension { get; set; }
    }
}
