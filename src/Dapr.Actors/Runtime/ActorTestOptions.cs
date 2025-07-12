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

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Actors.Runtime;

/// <summary>
/// Specifies optional settings settings for the <see cref="ActorHost" /> when creating an actor
/// instance for testing.
/// </summary>
public sealed class ActorTestOptions
{
    /// <summary>
    /// Gets or sets the <see cref="ActorId" />.
    /// </summary>
    /// <returns></returns>
    public ActorId ActorId { get; set; } = ActorId.CreateRandom();

    /// <summary>
    /// Gets or sets the <see cref="ILoggerFactory" />.
    /// </summary>
    /// <value></value>
    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

    /// <summary>
    /// Gets or sets the <see cref="JsonSerializerOptions" />.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    /// <summary>
    /// Gets or sets the <see cref="IActorProxyFactory" />.
    /// </summary>
    public IActorProxyFactory ProxyFactory { get; set; } = new InvalidProxyFactory();

    /// <summary>
    /// Gets or sets the <see cref="ActorTimerManager" />.
    /// </summary>
    public ActorTimerManager TimerManager { get; set; } = new InvalidTimerManager();

    private class InvalidProxyFactory : IActorProxyFactory
    {
        private static readonly string Message = 
            "This actor was initialized for tests without providing a replacement for the proxy factory. " +
            "Provide a mock implementation of 'IProxyFactory' by setting 'ActorTestOptions.ProxyFactory'."; 

        public ActorProxy Create(ActorId actorId, string actorType, ActorProxyOptions options = null)
        {
            throw new NotImplementedException(Message);
        }

        public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, string actorType, ActorProxyOptions options = null) where TActorInterface : IActor
        {
            throw new NotImplementedException(Message);
        }

        public object CreateActorProxy(ActorId actorId, Type actorInterfaceType, string actorType, ActorProxyOptions options = null)
        {
            throw new NotImplementedException(Message);
        }
    }

    private class InvalidTimerManager : ActorTimerManager
    {
        private static readonly string Message = 
            "This actor was initialized for tests without providing a replacement for the timer manager. " +
            "Provide a mock implementation of 'ActorTimerManager' by setting 'ActorTestOptions.TimerManager'."; 

        public override Task RegisterReminderAsync(ActorReminder reminder)
        {
            throw new NotImplementedException(Message);
        }

        public override Task RegisterTimerAsync(ActorTimer timer)
        {
            throw new NotImplementedException(Message);
        }

        public override Task<IActorReminder> GetReminderAsync(ActorReminderToken reminder)
        {
            throw new NotImplementedException(Message);
        }

        public override Task UnregisterReminderAsync(ActorReminderToken reminder)
        {
            throw new NotImplementedException(Message);
        }

        public override Task UnregisterTimerAsync(ActorTimerToken timer)
        {
            throw new NotImplementedException(Message);
        }
    }
}