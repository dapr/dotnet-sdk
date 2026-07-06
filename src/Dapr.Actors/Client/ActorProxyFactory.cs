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

namespace Dapr.Actors.Client;

using System;
using System.Net.Http;
using Dapr.Actors.Builder;
using Dapr.Actors.Communication;
using Dapr.Actors.Communication.Client;

/// <summary>
/// Represents a factory class to create a proxy to the remote actor objects.
/// </summary>
public class ActorProxyFactory : IActorProxyFactory
{
    private ActorProxyOptions defaultOptions;
    private readonly HttpMessageHandler handler;

    /// <inheritdoc/>
    public ActorProxyOptions DefaultOptions
    {
        get => this.defaultOptions;
        set
        {
            this.defaultOptions = value ??
                                  throw new ArgumentNullException(nameof(DefaultOptions), $"{nameof(ActorProxyFactory)}.{nameof(DefaultOptions)} cannot be null");
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
    /// </summary>
    [Obsolete("Use the constructor that accepts HttpMessageHandler. This will be removed in the future.")]
    public ActorProxyFactory(ActorProxyOptions options, HttpClientHandler handler)
        : this(options, (HttpMessageHandler)handler)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
    /// </summary>
    public ActorProxyFactory(ActorProxyOptions options = null, HttpMessageHandler handler = null)
    {
        this.defaultOptions = options ?? new ActorProxyOptions();
        this.handler = handler;
    }

    /// <inheritdoc/>
    public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, string actorType, ActorProxyOptions options = null)
        where TActorInterface : IActor
        => (TActorInterface)this.CreateActorProxy(actorId, typeof(TActorInterface), actorType, options ?? this.defaultOptions);

    /// <inheritdoc/>
    public ActorProxy Create(ActorId actorId, string actorType, ActorProxyOptions options = null)
    {
        options ??= this.DefaultOptions;

        var actorProxy = new ActorProxy();
        var daprInteractor = new DaprHttpInteractor(this.handler, options.HttpEndpoint, options.DaprApiToken, options.RequestTimeout);
        var nonRemotingClient = new ActorNonRemotingClient(daprInteractor);
        actorProxy.Initialize(nonRemotingClient, actorId, actorType, options);

        return actorProxy;
    }

    /// <inheritdoc/>
    public object CreateActorProxy(ActorId actorId, Type actorInterfaceType, string actorType, ActorProxyOptions options = null)
    {
        options ??= this.DefaultOptions;

        var daprInteractor = new DaprHttpInteractor(this.handler, options.HttpEndpoint, options.DaprApiToken, options.RequestTimeout);
            
        // provide a serializer if 'useJsonSerialization' is true and no serialization provider is provided.
        IActorMessageBodySerializationProvider serializationProvider = null;
        if (options.UseJsonSerialization)
        {
            serializationProvider = new ActorMessageBodyJsonSerializationProvider(options.JsonSerializerOptions);
        }

        var remotingClient = new ActorRemotingClient(daprInteractor, serializationProvider);
        var proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(actorInterfaceType);
        var actorProxy = proxyGenerator.CreateActorProxy();
        actorProxy.Initialize(remotingClient, actorId, actorType, options);

        return actorProxy;
    }
}