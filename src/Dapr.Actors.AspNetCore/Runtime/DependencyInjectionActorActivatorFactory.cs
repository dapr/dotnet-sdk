// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;

namespace Dapr.Actors.Runtime
{
    // Implementation of ActorActivatorFactory that uses Microsoft.Extensions.DependencyInjection.
    internal class DependencyInjectionActorActivatorFactory : ActorActivatorFactory
    {
        private readonly IServiceProvider services;

        public DependencyInjectionActorActivatorFactory(IServiceProvider services)
        {
            this.services = services;
        }

        public override ActorActivator CreateActivator(ActorTypeInformation type)
        {
            return new DependencyInjectionActorActivator(services, type);
        }
    }
}
