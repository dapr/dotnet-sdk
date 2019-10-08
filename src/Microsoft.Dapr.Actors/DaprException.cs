// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Exception for Dapr operations.
    /// </summary>
    [Serializable]
    public class DaprException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DaprException"/> class with error code <see cref="DaprErrorCodes.UNKNOWN"/>.
        /// </summary>
        public DaprException()
            : this(DaprErrorCodes.UNKNOWN, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprException"/> class with error code <see cref="DaprErrorCodes.UNKNOWN"/> and a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public DaprException(string message)
            : this(message, DaprErrorCodes.UNKNOWN, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprException"/> class with a specified error
        /// message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DaprException(string message, Exception innerException)
            : this(message, innerException, DaprErrorCodes.UNKNOWN, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprException"/> class with a specified error code.
        /// </summary>
        /// <param name="errorCode">The error code associated with the exception.</param>
        /// <param name="isTransient">True, if the exception is to be treated as an transient exception.</param>
        public DaprException(DaprErrorCodes errorCode, bool isTransient)
            : this(string.Empty, errorCode, isTransient)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprException"/> class with specified error message and error code.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="errorCode">The error code associated with the exception.</param>
        /// <param name="isTransient">Indicating if its an transient exception. </param>
        public DaprException(string message, DaprErrorCodes errorCode, bool isTransient)
            : this(message, null, errorCode, isTransient)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprException"/> class.
        /// Initializes a new instance of <see cref="DaprException" /> class
        /// with a specified error message, a reference to the inner exception that is the cause of this exception, and a specified error code.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception or null if no inner exception is specified. The <see cref="System.Exception" /> class provides more details about the inner exception..</param>
        /// <param name="errorCode">The error code associated with the exception.</param>
        /// <param name="isTransient">Indicating if its an transient exception. </param>
        public DaprException(string message, Exception inner, DaprErrorCodes errorCode, bool isTransient)
            : base(message, inner)
        {
            this.ErrorCode = errorCode;
            this.IsTransient = isTransient;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprException"/> class with a specified context.
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo" /> object that contains serialized object data of the exception being thrown.</param>
        /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext" /> object that contains contextual information about the source or destination. The context parameter is reserved for future use and can be null.</param>
        protected DaprException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info != null)
            {
                this.ErrorCode = (DaprErrorCodes)info.GetValue(nameof(this.ErrorCode), typeof(DaprErrorCodes));
                this.IsTransient = info.GetBoolean(nameof(this.IsTransient));
            }
        }

        /// <summary>
        /// Gets the error code parameter.
        /// </summary>
        /// <value>The error code associated with the <see cref="DaprException" /> exception.</value>
        public DaprErrorCodes ErrorCode { get; } = DaprErrorCodes.UNKNOWN;

        /// <summary>
        /// Gets a value indicating whether gets exception is Transient and operation can be retried.
        /// </summary>
        /// <value>Value indicating whether the exception is transient or not.</value>
        public bool IsTransient { get; } = false;

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (info != null)
            {
                info.AddValue(nameof(this.ErrorCode), this.ErrorCode);
                info.AddValue(nameof(this.IsTransient), this.IsTransient);
            }
        }
    }
}
