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
    using Microsoft.DurableTask.Client;
    using Microsoft.DurableTask.Worker;
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

#pragma warning disable CS0618 // Type or member is obsolete - keeping around temporarily - replaced by DaprWorkflowClient
            serviceCollection.TryAddSingleton<WorkflowEngineClient>();
#pragma warning restore CS0618 // Type or member is obsolete

            serviceCollection.TryAddSingleton<DaprWorkflowClient>();
            serviceCollection.AddDaprClient();
            serviceCollection.AddDaprWorkflowClient();

            serviceCollection.AddOptions<WorkflowRuntimeOptions>().Configure(configure);

            serviceCollection.AddDurableTaskWorker(builder =>
            {
                WorkflowRuntimeOptions options = new();
                configure?.Invoke(options);

                if (TryGetGrpcAddress(out string address))
                {
                    builder.UseGrpc(address);
                }
                else
                {
                    builder.UseGrpc();
                }

                builder.AddTasks(registry => options.AddWorkflowsAndActivitiesToRegistry(registry));
            });

            return serviceCollection;
        }

        /// <summary>
        /// Adds Dapr Workflow client support to the service collection.
        /// </summary>
        /// <remarks>
        /// Use this extension method if you want to use <see cref="DaprWorkflowClient"/> in your app
        /// but don't wish to define any workflows or activities.
        /// </remarks>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        public static IServiceCollection AddDaprWorkflowClient(this IServiceCollection services)
        {
            services.TryAddSingleton<DaprWorkflowClient>();
            services.AddDurableTaskClient(builder =>
            {
                if (TryGetGrpcAddress(out string address))
                {
                    builder.UseGrpc(address);
                }
                else
                {
                    builder.UseGrpc();
                }

                builder.RegisterDirectly();
            });

            return services;
        }

        static bool TryGetGrpcAddress(out string address)
        {
            // TODO: Ideally we should be using DaprDefaults.cs for this. However, there are two blockers:
            //   1. DaprDefaults.cs uses 127.0.0.1 instead of localhost, which prevents testing with Dapr on WSL2 and the app on Windows
            //   2. DaprDefaults.cs doesn't compile when the project has C# nullable reference types enabled.
            // If the above issues are fixed (ensuring we don't regress anything) we should switch to using the logic in DaprDefaults.cs.
            string? daprPortStr = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
            if (int.TryParse(daprPortStr, out int daprGrpcPort))
            {
                // There is a bug in the Durable Task SDK that requires us to change the format of the address
                // depending on the version of .NET that we're targeting. For now, we work around this manually.
#if NET6_0_OR_GREATER
                address = $"http://localhost:{daprGrpcPort}";
#else
                address = $"localhost:{daprGrpcPort}";
#endif
                return true;
            }

            address = string.Empty;
            return false;
        }
    }
}

