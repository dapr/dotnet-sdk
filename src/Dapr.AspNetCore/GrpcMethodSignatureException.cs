using System;
using System.Collections.Generic;
using System.Text;

namespace Dapr.AspNetCore
{
    /// <summary>
    /// method signature exception for grpc method
    /// </summary>
    public class GrpcMethodSignatureException : Exception
    {
        /// <summary>
        /// The default contructor
        /// </summary>
        public GrpcMethodSignatureException()
        {
            Type = ErrorType.Unknow;
        }

        /// <summary>
        /// The contructor with type
        /// </summary>
        public GrpcMethodSignatureException(ErrorType type)
        {
            Type = type;
        }

        /// <summary>
        /// The contructor with message
        /// </summary>
        /// <param name="message"></param>
        public GrpcMethodSignatureException(string message) : base(message)
        {
            Type = ErrorType.Unknow;
        }

        /// <summary>
        /// The contructor with type and message
        /// </summary>
        /// <param name="type"></param>
        /// <param name="message"></param>
        public GrpcMethodSignatureException(ErrorType type, string message) : base(message)
        {
            Type = type;
        }

        /// <summary>
        /// The contructor with message and innerException
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public GrpcMethodSignatureException(string message, Exception innerException) : base(message, innerException)
        {
            Type = ErrorType.Unknow;
        }

        /// <summary>
        /// error type of this exception
        /// </summary>
        public ErrorType Type { get; set; }

        /// <summary>
        /// The error type for <see cref="GrpcMethodSignatureException"/>
        /// </summary>
        public enum ErrorType
        {
            /// <summary>
            /// unknow
            /// </summary>
            Unknow,

            /// <summary>
            /// parameter's length is not right
            /// </summary>
            ParameterLength,

            /// <summary>
            /// parameter's type is not right
            /// </summary>
            ParameterType,

            /// <summary>
            /// return type is not right
            /// </summary>
            ReturnType,

            /// <summary>
            /// argument type of return generic type is not right
            /// </summary>
            ReturnGenericTypeArgument
        }
    }
}
