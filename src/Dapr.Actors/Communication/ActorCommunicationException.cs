// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml;
    using Dapr.Actors.Resources;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides an information about an exception from the service. This exception is thrown when the actual
    /// exception from the service cannot be serialized for transferring to client.
    /// </summary>
    public class ActorCommunicationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorCommunicationException"/> class.
        /// </summary>
        public ActorCommunicationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorCommunicationException" /> class with appropriate message.
        /// </summary>
        /// <param name="actualExceptionType">the ActualExceptionType of exception thrown.</param>
        /// <param name="message">The error message that explains the reason for this exception.
        /// </param>
        public ActorCommunicationException(string actualExceptionType, string message)
            : base(message)
        {
            this.ActualExceptionType = actualExceptionType;
        }

        /// <summary>
        /// Gets the ActualExceptionType is the type of actual exception thrown.
        /// </summary>
        public string ActualExceptionType { get; private set; }

        /// <summary>
        /// Factory method that constructs the ActorCommunicationException from an exception.
        /// </summary>
        /// <param name="exception">Exception.</param>
        /// <returns>Serialized bytes.</returns>
        public static (byte[], string) FromException(Exception exception)
        {
            try
            {
                return (Serialize(exception), String.Empty);
            }
            catch (Exception e)
            {
                // failed to serialize the exception, include the information about the exception in the data
                // Add trace diagnostics
                var errorMessage = $"ActorCommunicationException, Serialization failed for Exception Type {exception.GetType().FullName} : Reason  {e}";
                return (FromExceptionString(exception), errorMessage);
            }
        }

        /// <summary>
        /// Gets the exception from the ActorCommunicationException.
        /// </summary>
        /// <param name="bufferedStream">The stream that contains the serialized exception or exception message.</param>
        /// <param name="result">Exception from the remote side.</param>
        /// <returns>true if there was a valid exception, false otherwise.</returns>
        public static bool ToException(Stream bufferedStream, out Exception result)
        {
            // try to de-serialize the bytes in to exception requestMessage and create service exception
            if (ActorCommunicationException.TryDeserialize(bufferedStream, out result))
            {
                return true;
            }

            bufferedStream.Dispose();

            return false;
        }

        internal static byte[] FromExceptionString(Exception exception)
        {
            var exceptionStringBuilder = new StringBuilder();

            exceptionStringBuilder.AppendFormat(
                CultureInfo.CurrentCulture,
                SR.ErrorExceptionSerializationFailed1,
                exception.GetType().FullName);

            exceptionStringBuilder.AppendLine();

            exceptionStringBuilder.AppendFormat(
                CultureInfo.CurrentCulture,
                SR.ErrorExceptionSerializationFailed2,
                exception);
            return Serialize(exception);
        }

        internal static byte[] Serialize(Exception exception)
        {
            var exceptionData = new ActorCommunicationExceptionData(exception.GetType().FullName, exception.Message);
            var exceptionBytes = exceptionData.Serialize();

            return exceptionBytes;
        }


        internal static bool TryDeserialize(Stream data, out Exception result, ILogger logger = null)
        {
            try
            {
                data.Seek(0, SeekOrigin.Begin);
                if (ActorCommunicationExceptionData.TryDeserialize(data, out var eData))
                {
                    result = new ActorCommunicationException(eData.Type, eData.Message);
                    return true;
                }
            }
            catch (Exception e)
            {
                // swallowing the exception
                logger?.LogWarning("RemoteException", "DeSerialization failed : Reason  {0}", e);
            }

            result = null;
            return false;
        }
    }
}
