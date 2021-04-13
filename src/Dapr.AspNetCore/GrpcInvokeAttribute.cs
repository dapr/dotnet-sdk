using System;

namespace Dapr.AspNetCore
{
    /// <summary>
    /// Make a method as gRPC Service Invocation
    /// </summary>
    /// <remarks>
    /// Service Invocation method have two parameters, 
    /// the first parameter must be <see cref="Google.Protobuf.IMessage"/>,
    /// the secode parameter must be <see cref="Grpc.Core.ServerCallContext"/>,
    /// the return type must be <see cref="System.Threading.Tasks.Task{TResult}"/>, and TResult must be <see cref="Google.Protobuf.IMessage"/>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class GrpcInvokeAttribute : Attribute
    {
        /// <summary>
        /// Default constructor, method name is from Method self
        /// </summary>
        public GrpcInvokeAttribute()
            : this(null)
        {
        }

        /// <summary>
        /// Custom constructor, use parameter methodName to set method name
        /// </summary>
        /// <param name="methodName">The method name of grpc invocation</param>
        public GrpcInvokeAttribute(string methodName)
        {
            MethodName = methodName;
        }

        /// <summary>
        /// Mehtod name of grpc invocation
        /// </summary>
        public string MethodName { get; set; }
    }
}
