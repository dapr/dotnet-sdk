// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr;

using System;
using System.Runtime.Serialization;

/// <summary>
/// Exception for Dapr operations.
/// </summary>
[Serializable]
public class DaprApiException : DaprException
{        
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprApiException"/> class with error code 'UNKNOWN'"/>.
    /// </summary>
    public DaprApiException()
        : this(errorCode: null, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprApiException"/> class with error code 'UNKNOWN' and a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public DaprApiException(string message)
        : this(message, errorCode: null, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprApiException"/> class with a specified error
    /// message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DaprApiException(string message, Exception innerException)
        : this(message, innerException, errorCode: null, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprApiException"/> class with a specified error code.
    /// </summary>
    /// <param name="errorCode">The error code associated with the exception.</param>
    /// <param name="isTransient">True, if the exception is to be treated as an transient exception.</param>
    public DaprApiException(string errorCode, bool isTransient)
        : this(string.Empty, errorCode, isTransient)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprApiException"/> class with specified error message and error code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code associated with the exception.</param>
    /// <param name="isTransient">Indicating if its an transient exception. </param>
    public DaprApiException(string message, string errorCode, bool isTransient)
        : this(message, null, errorCode, isTransient)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprApiException"/> class.
    /// Initializes a new instance of <see cref="DaprApiException" /> class
    /// with a specified error message, a reference to the inner exception that is the cause of this exception, and a specified error code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception or null if no inner exception is specified. The <see cref="System.Exception" /> class provides more details about the inner exception..</param>
    /// <param name="errorCode">The error code associated with the exception.</param>
    /// <param name="isTransient">Indicating if its an transient exception. </param>
    public DaprApiException(string message, Exception inner, string errorCode, bool isTransient)
        : base(message, inner)
    {
        this.ErrorCode = errorCode ?? "UNKNOWN";
        this.IsTransient = isTransient;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprApiException"/> class with a specified context.
    /// </summary>
    /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo" /> object that contains serialized object data of the exception being thrown.</param>
    /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext" /> object that contains contextual information about the source or destination. The context parameter is reserved for future use and can be null.</param>
#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")] // add this attribute to the serialization ctor
#endif
    protected DaprApiException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        if (info != null)
        {
            this.ErrorCode = (string)info.GetValue(nameof(this.ErrorCode), typeof(string));
            this.IsTransient = info.GetBoolean(nameof(this.IsTransient));
        }
    }

    /// <summary>
    /// Gets the error code parameter.
    /// </summary>
    /// <value>The error code associated with the <see cref="DaprApiException" /> exception.</value>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets a value indicating whether gets exception is Transient and operation can be retried.
    /// </summary>
    /// <value>Value indicating whether the exception is transient or not.</value>
    public bool IsTransient { get; } = false;

    /// <inheritdoc />
#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")] // add this attribute to GetObjectData
#endif
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