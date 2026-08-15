// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.Core.Registration;

/// <summary>
/// Collects generated actor registrations before the service provider is built.
/// </summary>
public sealed class ActorRuntimeRegistrationBuilder
{
    private readonly List<ActorRuntimeRegistration> registrations = [];

    /// <summary>
    /// Adds an actor registration.
    /// </summary>
    public ActorRuntimeRegistrationBuilder Add(
        string actorType,
        Type interfaceType,
        Type implementationType,
        Func<IServiceProvider, ActorId, IActor> factory,
        IActorDispatcher dispatcher,
        ActorLifecycle? lifecycle = null,
        DaprActorsOptions? options = null,
        DaprActorTypeOptions? typeOptions = null)
    {
        registrations.Add(new ActorRuntimeRegistration(actorType, interfaceType, implementationType, factory, dispatcher, lifecycle, options, typeOptions));
        return this;
    }

    /// <summary>
    /// Adds an actor registration whose dispatcher is resolved from the final service provider.
    /// </summary>
    public ActorRuntimeRegistrationBuilder Add(
        string actorType,
        Type interfaceType,
        Type implementationType,
        Func<IServiceProvider, ActorId, IActor> factory,
        Func<IServiceProvider, IActorDispatcher> dispatcherFactory,
        ActorLifecycle? lifecycle = null,
        DaprActorsOptions? options = null,
        DaprActorTypeOptions? typeOptions = null)
    {
        registrations.Add(new ActorRuntimeRegistration(actorType, interfaceType, implementationType, factory, dispatcherFactory, lifecycle, options, typeOptions));
        return this;
    }

    internal ActorRuntimeRegistry Build(IServiceProvider services) => new(registrations, services);
}
