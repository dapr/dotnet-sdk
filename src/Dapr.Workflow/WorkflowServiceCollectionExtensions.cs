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

using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Workflow;

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
    /// <param name="lifetime">The lifetime of the registered services.</param>
    public static IServiceCollection AddDaprWorkflow(
        this IServiceCollection serviceCollection,
        Action<WorkflowRuntimeOptions> configure,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection, nameof(serviceCollection));

        serviceCollection.AddDaprClient(lifetime: lifetime);
        serviceCollection.AddHttpClient();
        serviceCollection.AddHostedService<WorkflowLoggingService>();

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                serviceCollection.TryAddSingleton<DaprWorkflowClient>();
                serviceCollection.TryAddSingleton<WorkflowRuntimeOptions>();
                break;
            case ServiceLifetime.Scoped:
                serviceCollection.TryAddScoped<DaprWorkflowClient>();
                serviceCollection.TryAddScoped<WorkflowRuntimeOptions>();
                break;
            case ServiceLifetime.Transient:
                serviceCollection.TryAddTransient<DaprWorkflowClient>();
                serviceCollection.TryAddTransient<WorkflowRuntimeOptions>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }

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
