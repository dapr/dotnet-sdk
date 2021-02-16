// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using Grpc.Core;
    using Grpc.Core.Testing;
    using Grpc.Core.Utils;
    using AppCallbackBase = AppCallback.Autogen.Grpc.v1.AppCallback.AppCallbackBase;

    // This client will forward requests to the AppCallback service implementation which then responds to the request
    public class AppCallbackClient : HttpClient
    {
        public AppCallbackClient(AppCallbackBase callbackService)
            : base(new Handler(callbackService))
        {
        }

        private class Handler : HttpMessageHandler
        {
            private readonly AppCallbackBase callbackService;

            public Handler(AppCallbackBase callbackService)
            {
                this.callbackService = callbackService;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest, CancellationToken cancellationToken)
            {
                var metadata = new Metadata();
                foreach (var (key, value) in httpRequest.Headers)
                {
                    metadata.Add(key, string.Join(",", value.ToArray()));
                }

                var context = TestServerCallContext.Create(
                    method: httpRequest.Method.Method,
                    host: httpRequest.RequestUri.Host,
                    deadline: DateTime.UtcNow.AddHours(1),
                    requestHeaders: metadata,
                    cancellationToken: cancellationToken,
                    peer: "127.0.0.1",
                    authContext: null,
                    contextPropagationToken: null,
                    writeHeadersFunc: _ => TaskUtils.CompletedTask,
                    writeOptionsGetter: () => new WriteOptions(),
                    writeOptionsSetter: writeOptions => {});

                var grpcRequest = await GrpcUtils.GetRequestFromRequestMessageAsync<InvokeServiceRequest>(httpRequest);
                var grpcResponse = await this.callbackService.OnInvoke(grpcRequest.Message, context);

                var streamContent = await GrpcUtils.CreateResponseContent(grpcResponse);
                var httpResponse = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);

                return httpResponse;
            }
        }
    }
}
