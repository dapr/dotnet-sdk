// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Dependency injection extensions for configuring workflow versioning runtime services.
/// </summary>
public static class WorkflowVersioningServiceCollectionExtensions
{
    /// <summary>
    /// Registers workflow versioning runtime services, including factories, resolver and diagnostics.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configure">Optional delegate to set global defaults via <see cref="WorkflowVersioningOptions"/>.</param>
    /// <returns></returns>
    public static IServiceCollection AddDaprWorkflowVersioning(
        this IServiceCollection services,
        Action<WorkflowVersioningOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        services.AddOptions<WorkflowVersioningOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.PostConfigure<WorkflowVersioningOptions>(opts =>
        {
            if (opts.DefaultStrategy is null)
            {
                opts.DefaultStrategy = sp =>
                {
                    var factory = sp.GetRequiredService<IWorkflowVersionStrategyFactory>();
                    return factory.Create(typeof(NumericVersionStrategy), canonicalName: "DEFAULT", optionsName: null, services: sp);
                };
            }

            if (opts.DefaultSelector is null)
            {
                opts.DefaultSelector = sp =>
                {
                    var factory = sp.GetRequiredService<IWorkflowVersionSelectorFactory>();
                    return factory.Create(typeof(MaxVersionSelector), canonicalName: "DEFAULT", optionsName: null, services: sp);
                };
            }
        });
        
        // Register singletons for options, diagnostics, factories and resolver
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkflowVersioningOptions>>().Value);
        services.AddSingleton<IWorkflowVersionDiagnostics, DefaultWorkflowVersioningDiagnostics>();
        services.AddSingleton<IWorkflowVersionStrategyFactory, DefaultWorkflowVersionStrategyFactory>();
        services.AddSingleton<IWorkflowVersionSelectorFactory, DefaultWorkflowVersionSelectorFactory>();
        services.AddSingleton<IWorkflowVersionResolver, WorkflowVersionResolver>();
        services.AddSingleton<IWorkflowRouterRegistry, WorkflowRouterRegistry>();
        AddVersioningHostedService(services);

        return services;
    }

    private static void AddVersioningHostedService(IServiceCollection services)
    {
        var alreadyRegistered = false;
        for (var i = 0; i < services.Count; i++)
        {
            var existing = services[i];
            if (existing.ServiceType == typeof(IHostedService) &&
                existing.ImplementationType == typeof(WorkflowVersioningRegistrationHostedService))
            {
                alreadyRegistered = true;
                break;
            }
        }

        if (alreadyRegistered)
        {
            return;
        }

        var descriptor = ServiceDescriptor.Singleton<IHostedService, WorkflowVersioningRegistrationHostedService>();
        var insertAt = -1;

        for (var i = 0; i < services.Count; i++)
        {
            var existing = services[i];
            if (existing.ServiceType != typeof(IHostedService))
                continue;

            if (string.Equals(existing.ImplementationType?.FullName, WorkflowVersioningRegistrationHostedService.WorkflowWorkerTypeName,
                    StringComparison.Ordinal))
            {
                insertAt = i;
                break;
            }
        }

        if (insertAt >= 0)
        {
            services.Insert(insertAt, descriptor);
        }
        else
        {
            services.Add(descriptor);
        }
    }
}
