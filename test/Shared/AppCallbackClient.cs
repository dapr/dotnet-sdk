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

namespace Dapr;

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