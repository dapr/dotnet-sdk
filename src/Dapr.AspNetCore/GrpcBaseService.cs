// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ---

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.AspNetCore
{
    /// <summary>
    /// Base dapr class for gRPC service in ASP.NET Core, application can use this base class to implement gRPC service for Dapr easily.
    /// </summary>
    public abstract class GrpcBaseService : global::Dapr.AppCallback.Autogen.Grpc.v1.AppCallback.AppCallbackBase
    {
        /// <summary>
        /// logger for GrpcBaseService
        /// </summary>
        protected readonly ILogger _logger;

        /// <summary>
        /// DaprClient for GrpcBaseService
        /// </summary>
        protected readonly DaprClient _daprClient;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="daprClient"></param>
        /// <param name="logger"></param>
        protected GrpcBaseService(DaprClient daprClient, ILogger logger)
        {
            _daprClient = daprClient;
            _logger = logger;

            InitServiceInvocation();
        }

        private object locker = new object();
        private static Dictionary<string, MethodInfo> invokeMethods;
        private void InitServiceInvocation()
        {
            lock (locker)
            {
                if (invokeMethods == null)
                {
                    invokeMethods = new Dictionary<string, MethodInfo>();
                    var methods = GetType().GetMethods(BindingFlags.Public);
                    foreach (var item in methods)
                    {
                        var att = item.GetCustomAttribute<GrpcInvokeAttribute>();
                        if (att != null)
                        {
                            var paras = item.GetParameters();

                            if (paras.Length != 2)
                                throw new MissingMethodException("Service Invocation method must have two parameters.");
                            if (!paras[0].ParameterType.IsSubclassOf(typeof(Google.Protobuf.IMessage)))
                                throw new MissingMethodException("The type of first parameter must derive from Google.Protobuf.IMessage.");
                            if (paras[0].ParameterType!=att.InputModelType)
                                throw new MissingMethodException("The type of first parameter must equals with InputModelType");
                            if (paras[1].ParameterType != typeof(ServerCallContext))
                                throw new MissingMethodException("The type of second parameter must be Grpc.CoreServerCallContext.");
                            if (item.ReturnType != typeof(Task<>))
                                throw new MissingMethodException("The return type must be Task<>.");

                            invokeMethods.Add((att.MethodName ?? item.Name).ToLower(), item);
                        }
                    }
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
                var method = invokeMethods[request.Method.ToLower()];
                var input = JsonSerializer.Deserialize(request.Data.Value.ToByteArray(), method.GetParameters()[0].ParameterType, jsonOptions);
                var task = (Task)method.Invoke(this, new object[] { input, context });
                await task;
                var output = task.GetType().GetProperty("Result").GetValue(task) as Google.Protobuf.IMessage;
                response.Data = Any.Pack(output);
                return response;
            }
            throw new MissingMethodException($"The method be not found: {request.Method}");
        }

        /// <summary>
        /// Implement ListTopicSubscriptions for the registration of pubsub consumer in derived class
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<ListTopicSubscriptionsResponse> ListTopicSubscriptions(Empty request, ServerCallContext context)
        {
            var result = new ListTopicSubscriptionsResponse();
            result.Subscriptions.Add(new TopicSubscription
            {
                PubsubName = "pubsub",
                Topic = "deposit"
            });
            result.Subscriptions.Add(new TopicSubscription
            {
                PubsubName = "pubsub",
                Topic = "withdraw"
            });
            return Task.FromResult(result);
        }

        /// <summary>
        /// Implement OnTopicEvent for the response topic in derived class
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<TopicEventResponse> OnTopicEvent(TopicEventRequest request, ServerCallContext context)
        {
            //if (request.PubsubName == "pubsub")
            //{
            //    var input = JsonSerializer.Deserialize<Models.Transaction>(request.Data.ToStringUtf8(), this.jsonOptions);
            //    var transaction = new GrpcServiceSample.Generated.Transaction() { Id = input.Id, Amount = (int)input.Amount, };
            //    if (request.Topic == "deposit")
            //    {
            //        await Deposit(transaction, context);
            //    }
            //    else
            //    {
            //        await Withdraw(transaction, context);
            //    }
            //}

            return await Task.FromResult(default(TopicEventResponse));
        }
    }
}
