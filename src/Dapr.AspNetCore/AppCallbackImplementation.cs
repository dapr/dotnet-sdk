using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf;
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
        /// logger for AppCallbackImplementation
        /// </summary>
        private readonly ILogger<AppCallbackImplementation> logger;

        /// <summary>
        /// The <see cref="IServiceProvider"/>, will use it to get instance of GrpcBaseService
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// DaprClient for AppCallbackImplementation
        /// </summary>
        private readonly DaprClient daprClient;

        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="daprClient"></param>
        /// <param name="logger"></param>
        /// <param name="serviceProvider"></param>
        public AppCallbackImplementation(DaprClient daprClient, ILogger<AppCallbackImplementation> logger, IServiceProvider serviceProvider)
        {
            this.daprClient = daprClient;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }

        internal static Dictionary<string, (System.Type, MethodInfo)> invokeMethods = new Dictionary<string, (System.Type, MethodInfo)>();
        internal static void ExtractGrpcInvoke(System.Type serviceType)
        {
            foreach (var item in serviceType.GetTypeInfo().DeclaredMethods)
            {
                var att = item.GetCustomAttribute<GrpcInvokeAttribute>();
                if (att != null)
                {
                    MethodHasOneParameter(item);
                    MethodFirstParameterIsIMessage(item);

                    if (item.ReturnType.IsGenericType)
                    {
                        MethodReturnTypeIsTaskGeneric(item);

                        MethodReturnGenericTypeFirstArgIsIMessage(item);
                    }
                    else
                    {
                        MethodReturnTypeIsTask(item);
                    }

                    invokeMethods[(att.MethodName ?? item.Name).ToLower()] = (serviceType, item);
                }
            }
        }

        #region Method Condition Functions
        private static void MethodReturnTypeIsTask(MethodInfo item)
        {
            if (item.ReturnType != typeof(Task))
            {
                throw new GrpcMethodSignatureException(GrpcMethodSignatureException.ErrorType.ReturnType,
                    "The return type must be Task or Task<>.");
            }
        }

        private static void MethodReturnGenericTypeFirstArgIsIMessage(MethodInfo item)
        {
            if (!typeof(IMessage).IsAssignableFrom(item.ReturnType.GenericTypeArguments[0]))
            {
                throw new GrpcMethodSignatureException(GrpcMethodSignatureException.ErrorType.ReturnGenericTypeArgument,
                    "The type of return type's generic type must derive from Google.Protobuf.IMessage.");
            }
        }

        private static void MethodReturnTypeIsTaskGeneric(MethodInfo item)
        {
            if (item.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
            {
                throw new GrpcMethodSignatureException(GrpcMethodSignatureException.ErrorType.ReturnType,
                    "The return type must be Task or Task<>.");
            }
        }

        private static void MethodFirstParameterIsIMessage(MethodInfo item)
        {
            if (!typeof(IMessage).IsAssignableFrom(item.GetParameters()[0].ParameterType))
            {
                throw new GrpcMethodSignatureException(GrpcMethodSignatureException.ErrorType.ParameterType,
                    "The type of first parameter must derive from Google.Protobuf.IMessage.");
            }
        }

        private static void MethodHasOneParameter(MethodInfo item)
        {
            if (item.GetParameters().Length != 1)
            {
                throw new GrpcMethodSignatureException(GrpcMethodSignatureException.ErrorType.ParameterLength,
                    "Service Invocation method must have only one parameter.");
            }
        }
        #endregion

        /// <summary>
        /// Implement OnIvoke for Service Invocation in derived class
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
        {
            var key = request.Method.ToLower();
            if (invokeMethods.ContainsKey(key))
            {
                try
                {
                    var (serviceType, method) = invokeMethods[key];
                    var serviceInstance = CreateServiceInstance(serviceType, context);

                    var input = Activator.CreateInstance(method.GetParameters()[0].ParameterType) as IMessage;
                    input.MergeFrom(request.Data.Value);
                    var task = (Task)method.Invoke(serviceInstance, new object[] { input });
                    await task;
                    var response = new InvokeResponse();
                    if (method.ReturnType.IsGenericType)
                    {
                        var output = task.GetType().GetProperty("Result").GetValue(task) as IMessage;
                        response.Data = Any.Pack(output);
                    }
                    return response;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message, ex);
                    context.Status = new Status(StatusCode.Internal, ex.Message);
                    return default;
                }
            }
            else
            {
                var warningMessage = $"The method for Service Invocation is not defined in this Grpc service: {request.Method}";
                logger.LogWarning(warningMessage);
                context.Status = new Status(StatusCode.NotFound, warningMessage);
            }
            return default;
        }

        internal static Dictionary<string, (System.Type, MethodInfo)> topicMethods = new Dictionary<string, (System.Type, MethodInfo)>();
        internal static void ExtractTopic(System.Type serviceType)
        {
            foreach (var item in serviceType.GetTypeInfo().DeclaredMethods)
            {
                var att = item.GetCustomAttribute<TopicAttribute>();
                if (att != null)
                {
                    MethodHasOneParameter(item);

                    MethodFirstParameterIsIMessage(item);

                    if (item.ReturnType.IsGenericType)
                    {
                        MethodReturnTypeIsTaskGeneric(item);
                    }
                    else
                    {
                        MethodReturnTypeIsTask(item);
                    }

                    topicMethods[GenerateKeyForTopic(att.PubsubName, att.Name)] = (serviceType, item);
                }
            }
        }

        private static string GenerateKeyForTopic(string pubsubName, string topic)
        {
            return $"{pubsubName}|{topic}";
        }


        /// <summary>
        /// implement ListTopicSubscriptions to register GrpcBaseService's topic method
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<ListTopicSubscriptionsResponse> ListTopicSubscriptions(Empty request, ServerCallContext context)
        {
            var result = new ListTopicSubscriptionsResponse();
            foreach (var key in topicMethods.Keys)
            {
                var split = key.Split('|');
                result.Subscriptions.Add(new TopicSubscription
                {
                    PubsubName = split[0],
                    Topic = split[1]
                });
            }
            return Task.FromResult(result);
        }

        /// <summary>
        /// implement OnTopicEvent to handle GrpcBaseService's topic method
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<TopicEventResponse> OnTopicEvent(TopicEventRequest request, ServerCallContext context)
        {
            var key = GenerateKeyForTopic(request.PubsubName, request.Topic);
            if (topicMethods.ContainsKey(key))
            {
                try
                {
                    var (serviceType, method) = topicMethods[key];
                    var serviceInstance = CreateServiceInstance(serviceType, context);

                    var input = JsonSerializer.Deserialize(request.Data.Span, method.GetParameters()[0].ParameterType, jsonOptions);
                    var task = (Task)method.Invoke(serviceInstance, new object[] { input });
                    await task;
                    return new TopicEventResponse();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message, ex);
                    return new TopicEventResponse { Status = TopicEventResponse.Types.TopicEventResponseStatus.Retry };
                }
            }
            else
            {
                logger.LogWarning($"The method for Topic is not defined in this Grpc service: {request.PubsubName} | {request.Topic}");
                return new TopicEventResponse { Status = TopicEventResponse.Types.TopicEventResponseStatus.Drop };
            }
        }

        private GrpcBaseService CreateServiceInstance(System.Type serviceType, ServerCallContext context)
        {
            if (!(serviceProvider.GetService(serviceType) is GrpcBaseService serviceInstance))
                throw new NullReferenceException("ServiceInstance is null or is not GrpcBaseService");

            serviceInstance.Context = context;
            serviceInstance.DaprClient = daprClient;

            return serviceInstance;
        }
    }
}
