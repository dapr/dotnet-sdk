// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr
{
    using System;

    /// <summary>
    /// A envelope to wrap the contents of the required properties for the Invoke endpoints.
    /// </summary>
    public sealed class InvokeEnvelope
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeEnvelope"/> class.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="data">The data to be sent within the body of the Dapr invoke request.</param>
        public InvokeEnvelope(string serviceName, string methodName, string data = "")
        {
            this.ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            this.MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
            this.Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Gets the name of the service the method should be invoked against.
        /// </summary>
        public string ServiceName { get; private set; }

        /// <summary>
        /// Gets tte name of the method to be invoked.
        /// </summary>
        public string MethodName { get; private set; }

        /// <summary>
        /// Gets the data to be posted with the request.
        /// </summary>
        public string Data { get; private set; }
    }
}