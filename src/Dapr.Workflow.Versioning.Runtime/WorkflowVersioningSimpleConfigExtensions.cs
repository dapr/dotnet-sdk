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
/// Convenience extension methods that make it easy to set global versioning defaults and configure named strategy
/// options, while internally delegating to the factory-based construction pipeline.
/// </summary>
public static class WorkflowVersioningSimpleConfigExtensions
{
    /// <summary>
    /// Sets the application's default workflow versioning strategy using the strategy factory, optionally specifying
    /// a named options scope to bind strategy configuration.
    /// </summary>
    /// <typeparam name="TStrategy">
    /// The concrete strategy type that implements <see cref="IWorkflowVersionStrategy"/>.
    /// </typeparam>
    /// <param name="services">The application's service collection.</param>
    /// <param name="optionsName">An optional options scope name used by the strategy factory to bind configuration
    /// for the default strategy (for example, <c>"app-defaults"</c>).</param>
    /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.UseDefaultWorkflowStrategy&lt;SemVerStrategy&gt;("app-defaults");
    /// services.AddOptions&lt;SemVerStrategyOptions&gt;("app-defaults")
    ///         .Configure(o =&gt; { o.AllowPrerelease = false; });
    /// </code>
    /// </example>
    public static IServiceCollection UseDefaultWorkflowStrategy<TStrategy>(
        this IServiceCollection services,
        string? optionsName = null)
        where TStrategy : class, IWorkflowVersionStrategy
    {
        services.PostConfigure<WorkflowVersioningOptions>(o =>
        {
            o.DefaultStrategy = sp =>
            {
                var factory = sp.GetRequiredService<IWorkflowVersionStrategyFactory>();
                return factory.Create(typeof(TStrategy), canonicalName: "DEFAULT", optionsName, services: sp);
            };
        });
        return services;

    }
    
    /// <summary>
    /// Sets the application's default workflow version selector using the selector factory,
    /// optionally specifying a named options scope to bind selector configuration.
    /// </summary>
    /// <typeparam name="TSelector">
    /// The concrete selector type that implements <see cref="IWorkflowVersionSelector"/>.
    /// </typeparam>
    /// <param name="services">The application's service collection.</param>
    /// <param name="optionsName">
    /// An optional options scope name used by the selector factory to bind configuration for the
    /// default selector (for example, <c>"app-defaults"</c>).
    /// </param>
    /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// services.UseDefaultWorkflowSelector&lt;MaxVersionSelector&gt;("app-defaults");
    /// </code>
    /// </example>
    public static IServiceCollection UseDefaultWorkflowSelector<TSelector>(
        this IServiceCollection services,
        string? optionsName = null)
        where TSelector : class, IWorkflowVersionSelector
    {
        ArgumentNullException.ThrowIfNull(services);

        services.PostConfigure<WorkflowVersioningOptions>(o =>
        {
            o.DefaultSelector = sp =>
            {
                var factory = sp.GetRequiredService<IWorkflowVersionSelectorFactory>();
                return factory.Create(typeof(TSelector), canonicalName: "DEFAULT", optionsName, services: sp);
            };
        });

        return services;
    }

    /// <summary>
    /// Configures named options for a strategy options type using the standard options pattern.
    /// This helper is provided for ergonomic parity with "Option A" style configuration while
    /// remaining compatible with the factory-based instantiation.
    /// </summary>
    /// <typeparam name="TOptions">The strategy options POCO type.</typeparam>
    /// <param name="services">The application's service collection.</param>
    /// <param name="name">The named options scope (for example, <c>"orders"</c>).</param>
    /// <param name="configure">An action that configures the named options instance.</param>
    /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.ConfigureStrategyOptions&lt;SemVerStrategyOptions&gt;("orders", o =&gt;
    /// {
    ///     o.AllowPrerelease = true;
    ///     o.BranchFilter = "main";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection ConfigureStrategyOptions<TOptions>(
        this IServiceCollection services,
        string name,
        Action<TOptions> configure)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<TOptions>(name).Configure(configure);
        return services;
    }
}
