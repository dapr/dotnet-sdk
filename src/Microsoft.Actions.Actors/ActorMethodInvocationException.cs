﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;

    /// <summary>
    /// Exception for Remote Actor Method Invocation.
    /// </summary>
    [Serializable]
    public class ActorMethodInvocationException : ActionsException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorMethodInvocationException"/> class.
        /// </summary>
        public ActorMethodInvocationException()
            : base(ActionsErrorCodes.ERR_INVOKE_ACTOR, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorMethodInvocationException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="isTransient">True, if the exception is to be treated as an transient exception.</param>
        public ActorMethodInvocationException(string message, bool isTransient)
            : base(message, ActionsErrorCodes.ERR_INVOKE_ACTOR, isTransient)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorMethodInvocationException"/> class with a specified error
        /// message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="isTransient">True, if the exception is to be treated as an transient exception.</param>
        public ActorMethodInvocationException(string message, Exception innerException, bool isTransient)
            : base(message, innerException, ActionsErrorCodes.ERR_INVOKE_ACTOR, isTransient)
        {
        }
    }
}
