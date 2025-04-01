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

namespace Dapr.Actors;

using System;
using System.IO;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides information about an exception from the actor service.
/// </summary>
public class ActorInvokeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorInvokeException"/> class.
    /// </summary>
    public ActorInvokeException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorInvokeException" /> class with appropriate message.
    /// </summary>
    /// <param name="actualExceptionType">The fully-qualified name of the exception type thrown by the actor.</param>
    /// <param name="message">The error message that explains the reason for this exception.
    /// </param>
    public ActorInvokeException(string actualExceptionType, string message)
        : base(message)
    {
        this.ActualExceptionType = actualExceptionType;
    }

    /// <summary>
    /// Gets the fully-qualified name of the exception type thrown by the actor.
    /// </summary>
    public string ActualExceptionType { get; private set; }

    /// <summary>
    /// Factory method that constructs the ActorInvokeException from an exception.
    /// </summary>
    /// <param name="exception">Exception.</param>
    /// <returns>Serialized bytes.</returns>
    internal static byte[] FromException(Exception exception)
    {
        var exceptionData = new ActorInvokeExceptionData(exception.GetType().FullName, exception.Message);
        var exceptionBytes = exceptionData.Serialize();

        return exceptionBytes;
    }

    /// <summary>
    /// Gets the exception from the ActorInvokeException.
    /// </summary>
    /// <param name="stream">The stream that contains the serialized exception or exception message.</param>
    /// <param name="result">Exception from the remote side.</param>
    /// <returns>true if there was a valid exception, false otherwise.</returns>
    internal static bool ToException(Stream stream, out Exception result)
    {
        // try to de-serialize the bytes in to exception requestMessage and create service exception
        if (ActorInvokeException.TryDeserialize(stream, out result))
        {
            return true;
        }

        return false;
    }

    internal static bool TryDeserialize(Stream stream, out Exception result, ILogger logger = null)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            var eData = ActorInvokeExceptionData.Deserialize(stream);
            result = new ActorInvokeException(eData.Type, eData.Message);
            return true;
        }
        catch (Exception e)
        {
            // swallowing the exception
            logger?.LogWarning("RemoteException: DeSerialization failed : Reason {0}", e);
        }

        result = null;
        return false;
    }
}