// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// 
/// </summary>
public abstract class DaprPublishSubscribeClient
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static DaprPublishSubscribeClient Create()
    {
        string? daprGrpcEndpoint = Environment.GetEnvironmentVariable("DAPR_GRPC_ENDPOINT");
        
        if (daprGrpcEndpoint is null)
        {
            string daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "50001";
            
            daprGrpcEndpoint = $"http://localhost:{daprGrpcPort}";
        }
        
        GrpcChannel channel = GrpcChannel.ForAddress(daprGrpcEndpoint);

        return new DaprPublishSubscribeGrpcClient(channel);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pubSubName"></param>
    /// <param name="topicName"></param>
    /// <param name="handler"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task SubscribeAsync(string pubSubName, string topicName, TopicRequestHandler handler, DaprSubscriptionOptions? options = null, CancellationToken cancellationToken = default);
}
