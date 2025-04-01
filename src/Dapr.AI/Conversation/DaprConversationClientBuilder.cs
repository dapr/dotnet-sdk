﻿// ------------------------------------------------------------------------
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

using Dapr.Common;
using Microsoft.Extensions.Configuration;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1.Dapr;

namespace Dapr.AI.Conversation;

/// <summary>
/// Used to create a new instance of a <see cref="DaprConversationClient"/>.
/// </summary>
public sealed class DaprConversationClientBuilder : DaprGenericClientBuilder<DaprConversationClient>
{
    /// <summary>
    /// Used to initialize a new instance of the <see cref="DaprConversationClient"/>.
    /// </summary>
    /// <param name="configuration"></param>
    public DaprConversationClientBuilder(IConfiguration? configuration) : base(configuration)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public DaprConversationClientBuilder() : base(null)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Builds the client instance from the properties of the builder.
    /// </summary>
    /// <returns>The Dapr client instance.</returns>
    /// <summary>
    /// Builds the client instance from the properties of the builder.
    /// </summary>
    public override DaprConversationClient Build()
    {
        var daprClientDependencies = BuildDaprClientDependencies(typeof(DaprConversationClient).Assembly);
        var client = new Autogenerated.DaprClient(daprClientDependencies.channel);
        return new DaprConversationClient(client, daprClientDependencies.httpClient, daprClientDependencies.daprApiToken);
    }
}
