// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Communication
{
    using System;

    /// <summary>
    /// Provides an information about an exception from the service. This exception is thrown when the actual
    /// exception from the service cannot be serialized for transferring to client.
    /// </summary>
    public class ServiceException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceException"/> class.
        /// </summary>
        public ServiceException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceException" /> class with appropriate message.
        /// </summary>
        /// <param name="actualExceptionType">the ActualExceptionType of exception thrown.</param>
        /// <param name="message">The error message that explains the reason for this exception.
        /// </param>
        public ServiceException(string actualExceptionType, string message)
            : base(message)
        {
            this.ActualExceptionType = actualExceptionType;
        }

        /// <summary>
        /// Gets the ActualExceptionType is the type of actual exception thrown.
        /// </summary>
        public string ActualExceptionType { get; private set; }
    }
}
