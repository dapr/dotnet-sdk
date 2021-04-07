using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.AspNetCore
{
    /// <summary>
    /// Default implementaion for <see cref="global::Dapr.AppCallback.Autogen.Grpc.v1.AppCallback.AppCallbackBase"/>
    /// </summary>
    public class AppCallbackImplementation : global::Dapr.AppCallback.Autogen.Grpc.v1.AppCallback.AppCallbackBase
    {
        /// <summary>
        /// logger for GrpcBaseService
        /// </summary>
        private readonly ILogger<AppCallbackImplementation> _logger;

        /// <summary>
        /// DaprClient for GrpcBaseService
        /// </summary>
        private readonly DaprClient _daprClient;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="daprClient"></param>
        /// <param name="logger"></param>
        public AppCallbackImplementation(DaprClient daprClient, ILogger<AppCallbackImplementation> logger)
        {
            _daprClient = daprClient;
            _logger = logger;
        }

        internal static Dictionary<string, (System.Type, MethodInfo)> invokeMethods = new Dictionary<string, (System.Type, MethodInfo)>();
        internal static void ExtractMethodInfoFromGrpcBaseService(System.Type serviceType)
        {
            foreach (var item in serviceType.GetTypeInfo().DeclaredMethods)
            {
                var att = item.GetCustomAttribute<GrpcInvokeAttribute>();
                if (att != null)
                {
                    var paras = item.GetParameters();

                    if (paras.Length != 2)
                        throw new MissingMethodException("Service Invocation method must have two parameters.");
                    if (!paras[0].ParameterType.IsSubclassOf(typeof(Google.Protobuf.IMessage)))
                        throw new MissingMethodException("The type of first parameter must derive from Google.Protobuf.IMessage.");
                    if (paras[0].ParameterType != att.InputModelType)
                        throw new MissingMethodException("The type of first parameter must equals with InputModelType");
                    if (paras[1].ParameterType != typeof(ServerCallContext))
                        throw new MissingMethodException("The type of second parameter must be Grpc.CoreServerCallContext.");
                    if (item.ReturnType != typeof(Task<>))
                        throw new MissingMethodException("The return type must be Task<>.");
                    if (!item.ReturnType.GetGenericTypeDefinition().IsSubclassOf(typeof(Google.Protobuf.IMessage)))
                        throw new MissingMethodException("The type of return type's generic type must derive from Google.Protobuf.IMessage.");

                    invokeMethods[(att.MethodName ?? item.Name).ToLower()] = (serviceType.GetType(), item);
                }
            }
        }

        static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>
        /// Implement OnIvoke for Service Invocation in derived class
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
        {
            if (invokeMethods.ContainsKey(request.Method.ToLower()))
            {
                var response = new InvokeResponse();
                var (serviceType,method) = invokeMethods[request.Method.ToLower()];
                var serviceInstance = Activator.CreateInstance(serviceType);
                var input = JsonSerializer.Deserialize(request.Data.Value.ToByteArray(), method.GetParameters()[0].ParameterType, jsonOptions);
                var task = (Task)method.Invoke(serviceInstance, new object[] { input, context });
                await task;
                var output = task.GetType().GetProperty("Result").GetValue(task) as Google.Protobuf.IMessage;
                response.Data = Any.Pack(output);
                return response;
            }
            throw new MissingMethodException($"The method be not found: {request.Method}");
        }
    }
}
