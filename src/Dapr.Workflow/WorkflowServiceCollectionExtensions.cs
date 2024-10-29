// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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

using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace Dapr.Workflow
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Contains extension methods for using Dapr Workflow with dependency injection.
    /// </summary>
    public static class WorkflowServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Dapr Workflow support to the service collection.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configure">A delegate used to configure actor options and register workflow functions.</param>
        public static IServiceCollection AddDaprWorkflow(
            this IServiceCollection serviceCollection,
            Action<WorkflowRuntimeOptions> configure)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            serviceCollection.TryAddSingleton<WorkflowRuntimeOptions>();
            serviceCollection.AddHttpClient();

#pragma warning disable CS0618 // Type or member is obsolete - keeping around temporarily - replaced by DaprWorkflowClient
            serviceCollection.TryAddSingleton<WorkflowEngineClient>();
#pragma warning restore CS0618 // Type or member is obsolete
            serviceCollection.AddHostedService<WorkflowLoggingService>();
            serviceCollection.TryAddSingleton<DaprWorkflowClient>();
            serviceCollection.AddDaprClient();
            
            serviceCollection.AddOptions<WorkflowRuntimeOptions>().Configure(configure);
            
            //Register the factory and force resolution so the Durable Task client and worker can be registered
            using (var scope = serviceCollection.BuildServiceProvider().CreateScope())
            {
                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                var configuration = scope.ServiceProvider.GetService<IConfiguration>();
                
                var factory = new DaprWorkflowClientBuilderFactory(configuration, httpClientFactory);
                factory.CreateClientBuilder(serviceCollection, configure);
            }

            return serviceCollection;
        }
    }
}
