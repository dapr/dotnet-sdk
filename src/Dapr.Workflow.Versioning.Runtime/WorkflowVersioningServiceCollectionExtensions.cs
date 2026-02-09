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
        
        // Options container for defaults
        var opts = new WorkflowVersioningOptions();
        configure?.Invoke(opts);
        
        // Register singletons for options, diagnostics, factories and resolver
        services.AddSingleton(opts);
        services.AddSingleton<IWorkflowVersionDiagnostics, DefaultWorkflowVersioningDiagnostics>();
        services.AddSingleton<IWorkflowVersionStrategyFactory, DefaultWorkflowVersionStrategyFactory>();
        services.AddSingleton<IWorkflowVersionSelectorFactory, DefaultWorkflowVersionSelectorFactory>();
        services.AddSingleton<IWorkflowVersionResolver, WorkflowVersionResolver>();

        return services;
    }
}
