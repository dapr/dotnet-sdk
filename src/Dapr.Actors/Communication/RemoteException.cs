// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Formatters;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using System.Text.Json;
    using System.Xml;
    using Dapr.Actors;
    using Dapr.Actors.Resources;
    using Microsoft.Extensions.Logging;
    using System.Reflection;

    /// <summary>
    /// Fault type used by Service Remoting to transfer the exception details from the service to the client.
    /// </summary>
    [DataContract(Name = "RemoteException", Namespace = Constants.Namespace)]
    internal class RemoteException
    {
        private static readonly DataContractSerializer ServiceExceptionDataSerializer = new DataContractSerializer(typeof(ServiceExceptionData));
        private static readonly string ExceptionClassName = "ClassName";
        private static readonly string ExceptionMessage = "Message";
        private static readonly string ExceptionSource = "Source";
        private static readonly string ExceptionHResult = "HResult";
        private static readonly string ExceptionInner = "InnerException";
        private static readonly string ExceptionData = "Data";


        static RemoteException()
        {
        }

        public RemoteException(List<ArraySegment<byte>> buffers)
        {
            this.Data = buffers;
        }

        /// <summary>
        /// Gets serialized exception or the exception message encoded as UTF8 (if the exception cannot be serialized).
        /// </summary>
        /// <value>Serialized exception or exception message.</value>
        [DataMember(Name = "Data", Order = 0)]
        public List<ArraySegment<byte>> Data { get; private set; }

        /// <summary>
        /// Factory method that constructs the RemoteException from an exception.
        /// </summary>
        /// <param name="exception">Exception.</param>
        /// <returns>Serialized bytes.</returns>
        public static (byte[], string) FromException(Exception exception)
        {
            try
            {
                var dict = new Dictionary<string, object>();

                var type = exception.GetType();
                dict[ExceptionClassName] = type.FullName;
                dict[ExceptionMessage] = exception.Message;
                dict[ExceptionData] = exception.Data;
                dict[ExceptionHResult] = exception.HResult;
                dict[ExceptionSource] = exception.Source;

                var innerExceptionDict = new Dictionary<string, object>();
                innerExceptionDict[ExceptionMessage] = exception.InnerException.Message;
                innerExceptionDict[ExceptionClassName] = exception.InnerException.GetType().FullName;
                innerExceptionDict[ExceptionData] = exception.InnerException.Data;
                innerExceptionDict[ExceptionSource] = exception.InnerException.Source;
                innerExceptionDict[ExceptionHResult] = exception.InnerException.HResult;

                dict[ExceptionInner] = innerExceptionDict;

                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!dict.ContainsKey(p.Name))
                        dict[p.Name] = p.GetValue(exception);
                }
                return (JsonSerializer.SerializeToUtf8Bytes(dict), string.Empty);

            }
            catch (Exception e)
            {
                // failed to serialize the exception, include the information about the exception in the data
                // Add trace diagnostics
                var errorMessage = $"RemoteException, Serialization failed for Exception Type {exception.GetType().FullName} : Reason  {e}";
                return (FromExceptionString(exception), errorMessage);
            }
        }

        /// <summary>
        /// Gets the exception from the RemoteException.
        /// </summary>
        /// <param name="bufferedStream">The stream that contains the serialized exception or exception message.</param>
        /// <param name="result">Exception from the remote side.</param>
        /// <returns>true if there was a valid exception, false otherwise.</returns>
        public static bool ToException(Stream bufferedStream, out Exception result)
        {
            // try to de-serialize the bytes in to the exception
            if (TryDeserializeException(bufferedStream, out var res))
            {
                result = res;
                return true;
            }

            // try to de-serialize the bytes in to exception requestMessage and create service exception
            if (TryDeserializeServiceException(bufferedStream, out result))
            {
                return true;
            }

            // Set Reason for Serialization failure. This can happen in case where serialization succeded
            // but deserialization fails as type is not accessible
            result = res;
            bufferedStream.Dispose();

            return false;
        }

        internal static bool TryDeserializeExceptionData(Stream data, out ServiceExceptionData result, ILogger logger = null)
        {
            try
            {
                var exceptionData = (ServiceExceptionData)DeserializeServiceExceptionData(data);
                result = exceptionData;
                return true;
            }
            catch (Exception e)
            {
                // swallowing the exception
                logger?.LogWarning(
                    "RemoteException",
                    " ServiceExceptionData DeSerialization failed : Reason  {0}",
                    e);
            }

            result = null;
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
            var exceptionData = new ServiceExceptionData(exception.GetType().FullName, exceptionStringBuilder.ToString());

            var exceptionBytes = SerializeServiceExceptionData(exceptionData);

            return exceptionBytes;
        }

        private static bool TryDeserializeException(Stream data, out Exception result)
        {
            try
            {
                using (var sr = new StreamReader(data, Encoding.UTF8))
                {
                    var str = sr.ReadToEnd();
                    var dict = (Dictionary<string, object>)JsonSerializer.Deserialize(str, typeof(Dictionary<string, object>));
                    var innerExceptionDict = (Dictionary<string, object>)JsonSerializer.Deserialize(dict[ExceptionInner].ToString(), typeof(Dictionary<string, object>));

                    var innerExceptionType = Type.GetType(innerExceptionDict[ExceptionClassName].ToString());
                    var innerException = (Exception)Activator.CreateInstance(innerExceptionType, innerExceptionDict[ExceptionMessage].ToString());
                    innerException.Source = innerExceptionDict[ExceptionSource]?.ToString();
                    innerException.HResult = Convert.ToInt32(innerExceptionDict[ExceptionHResult].ToString());

                    var exceptionType = Type.GetType(dict[ExceptionClassName].ToString());
                    result = (Exception)Activator.CreateInstance(exceptionType, dict[ExceptionMessage].ToString(), innerException);
                    result.Source = dict[ExceptionSource]?.ToString();
                    result.HResult = Convert.ToInt32(dict[ExceptionHResult].ToString());

                    return true;
                }
            }
            catch (Exception ex)
            {
                // return reason for serialization failure
                result = ex;
                return false;
            }
        }

        private static bool TryDeserializeServiceException(Stream data, out Exception result, ILogger logger = null)
        {
            try
            {
                data.Seek(0, SeekOrigin.Begin);
                if (TryDeserializeExceptionData(data, out var eData))
                {
                    result = new ServiceException(eData.Type, eData.Message);
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

        private static object DeserializeServiceExceptionData(Stream buffer)
        {
            if ((buffer == null) || (buffer.Length == 0))
            {
                return null;
            }

            using var reader = XmlDictionaryReader.CreateBinaryReader(buffer, XmlDictionaryReaderQuotas.Max);
            return ServiceExceptionDataSerializer.ReadObject(reader);
        }

        private static byte[] SerializeServiceExceptionData(ServiceExceptionData msg)
        {
            if (msg == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            using var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
            ServiceExceptionDataSerializer.WriteObject(writer, msg);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
