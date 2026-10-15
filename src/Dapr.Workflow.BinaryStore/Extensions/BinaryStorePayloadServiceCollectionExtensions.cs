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

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Workflow.BinaryStore.Extensions;

/// <summary>
/// Registers binary-store-backed payload offloading for Dapr Workflows.
/// </summary>
public static class BinaryStorePayloadServiceCollectionExtensions
{
    /// <summary>
    /// Enables offloading of large workflow payloads to a Dapr binary store. Replaces the default
    /// <see cref="IWorkflowPayloadConverter"/>, attaches an <see cref="IWorkflowPayloadResolver"/>
    /// feature to every orchestrator execution, and registers the resolver and
    /// <see cref="WorkflowPayloadCleaner"/> for injection into activities and clients.
    /// </summary>
    /// <param name="services">The service collection. <c>AddDaprWorkflow</c> must already have been called.</param>
    /// <param name="configure">Configures the offloading options. <see cref="StoreName"/> is required.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection WithBinaryStorePayloads(this IServiceCollection services, Action<BinaryStorePayloadOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
 
        services.AddOptions<BinaryStorePayloadOptions>()
            .Configure(configure)
            .Validate(o => { o.Validate(); return true; });
 
        // Ensure a binary store client is available without overriding a user's own registration.
        services.AddDaprBinaryStoreClient();
 
        services.TryAddSingleton<IWorkflowPayloadManifest>(NullWorkflowPayloadManifest.Instance);
        services.AddSingleton<BinaryStorePayloadConverter>();
        services.Replace(ServiceDescriptor.Singleton<IWorkflowPayloadConverter>(sp => sp.GetRequiredService<BinaryStorePayloadConverter>()));
        services.TryAddSingleton<IWorkflowPayloadResolver>(sp => sp.GetRequiredService<BinaryStorePayloadConverter>());
        services.AddSingleton<IWorkflowFeatureProvider, BinaryStorePayloadFeatureProvider>();
        services.TryAddSingleton<WorkflowPayloadCleaner>();
 
        return services;
    }
 
    /// <summary>
    /// Registers a durable <see cref="IWorkflowPayloadManifest"/> so that purging a workflow
    /// instance also deletes its offloaded payloads. Call after <see cref="WithBinaryStorePayloads"/>.
    /// </summary>
    /// <typeparam name="TManifest">The manifest implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection WithPayloadManifest<TManifest>(this IServiceCollection services)
        where TManifest : class, IWorkflowPayloadManifest
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IWorkflowPayloadManifest, TManifest>());
        return services;
    }
}
 
