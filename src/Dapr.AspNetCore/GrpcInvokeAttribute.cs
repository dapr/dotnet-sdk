using System;
using System.Collections.Generic;
using System.Text;

namespace Dapr.AspNetCore
{
    /// <summary>
    /// Make a method as gRPC Service Invocation
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class GrpcInvokeAttribute : Attribute
    {
        /// <summary>
        /// Default constructor, method name is from Method self
        /// </summary>
        /// <param name="inputModelType"></param>
        public GrpcInvokeAttribute(Type inputModelType)
            : this(inputModelType, null)
        {
        }

        /// <summary>
        /// Custom constructor, use parameter methodName to set method name
        /// </summary>
        /// <param name="inputModelType"></param>
        /// <param name="methodName">The method name of grpc invocation</param>
        public GrpcInvokeAttribute(Type inputModelType, string methodName)
        {
            if (inputModelType is null)
            {
                throw new ArgumentNullException(nameof(inputModelType));
            }

            if (!inputModelType.IsSubclassOf(typeof(Google.Protobuf.IMessage)))
                throw new ArgumentException("inputModelType must derive from Google.Protobuf.IMessage");

            InputModelType = inputModelType;

            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            MethodName = methodName;
        }

        /// <summary>
        /// The type of input model, must be the <see cref="Google.Protobuf.IMessage{T}"/>
        /// </summary>
        public Type InputModelType { get; set; }

        /// <summary>
        /// Mehtod name of grpc invocation
        /// </summary>
        public string MethodName { get; set; }
    }
}
