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

            serviceCollection.AddHostedService<WorkflowLoggingService>();
            
            serviceCollection.TryAddSingleton<DaprWorkflowClient>();
            serviceCollection.AddDaprClient();
            
            serviceCollection.AddOptions<WorkflowRuntimeOptions>().Configure(configure);

            serviceCollection.AddSingleton(c =>
            {
                var factory = c.GetRequiredService<DaprWorkflowClientBuilderFactory>();
                factory.CreateClientBuilder(configure);
                return new object(); //Placeholder as actual registration is performed inside factory
            });

            return serviceCollection;
        }
    }
}
