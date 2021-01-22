// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;

namespace Dapr
{
    /// <summary>
    /// The base type of exceptions thrown by the Dapr .NET SDK.
    /// </summary>
    public class DaprException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DaprException" /> with the provided <paramref name="message" />.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public DaprException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="DaprException" /> with the provided 
        /// <paramref name="message" /> and <paramref name="innerException" />.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public DaprException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
