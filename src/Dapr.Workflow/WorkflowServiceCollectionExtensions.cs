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
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.DurableTask.Client;
    using Microsoft.DurableTask.Worker;
    using System.Collections.Generic;

    /// <summary>
    /// Contains extension methods for using Dapr Workflow with dependency injection.
    /// </summary>
    public static class WorkflowServiceCollectionExtensions
    {
        /// <summary>
        ///  Determines whether all element of a sequence fail to satisfy a condition -- or if none satisfies a condition.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
        /// <param name="source">
        /// An System.Collections.Generic.IEnumerable`1 whose elements to apply the predicate to.
        /// </param>
        /// <param name="predicate">A function to test each element for a condition.</param>
        /// <returns>
        /// true if all elements in the source sequence fail the test in the specified predicate; otherwise, true
        /// </returns>
        static bool None<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) => !source.Any(predicate);

        /// <summary>
        /// Adds a singleton service of the type specified in <paramref name="TService"/> to the
        /// specified <see cref="IServiceCollection"/> <b>if and only if</b> it isn't already present.
        /// </summary>
        /// <typeparam name="TService">The type of the service to register and the implementation to use.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        static IServiceCollection AddSingletonIfNotPresent<TService>(this IServiceCollection services)
        where TService : class
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (services.None(s => s.ImplementationType == typeof(TService)))
            {
                return services.AddSingleton(typeof(TService));
            }

            return services;
        }

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

            serviceCollection.AddSingletonIfNotPresent<WorkflowRuntimeOptions>();
            serviceCollection.AddSingletonIfNotPresent<WorkflowClient>();
            serviceCollection.AddDaprClient();

            serviceCollection.AddDurableTaskClient(builder =>
            {
                builder.UseGrpc();
                builder.RegisterDirectly();
            });

            serviceCollection.AddDurableTaskWorker(builder =>
            {
                WorkflowRuntimeOptions options = new();
                configure?.Invoke(options);

                builder.UseGrpc();
                builder.AddTasks(registry => options.AddWorkflowsToRegistry(registry));
            });

            return serviceCollection;
        }
    }
}

