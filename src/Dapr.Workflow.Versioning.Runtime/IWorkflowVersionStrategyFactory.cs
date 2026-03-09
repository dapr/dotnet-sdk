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
/// Creates configured <see cref="IWorkflowVersionStrategy"/> instances based on the workflow
/// metadata discovered at compile time and the application's dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// The versioning runtime invokes this factory when it needs a strategy instance for either:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
/// A specific workflow family that declared <see cref="WorkflowVersionAttribute.StrategyType"/>,
/// optionally with WorkflowVersionAttribute.OptionsName; or
///     </description>
///   </item>
///   <item>
///     <description>
/// The global default strategy as configured via <see cref="WorkflowVersioningOptions.DefaultStrategy"/>.
///     </description>
///   </item>
/// </list>
/// <para>
/// Implementations typically construct the strategy via
/// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider,Type,object[])"/> so
/// constructor injection works, then bind any named options, configuration, or external policy.
/// </para>
/// </remarks>
public interface IWorkflowVersionStrategyFactory
{
    /// <summary>
    /// Creates a configured <see cref="IWorkflowVersionStrategy"/> for the given workflow family.
    /// </summary>
    /// <param name="strategyType">
    /// The concrete strategy type to instantiate. Must implement <see cref="IWorkflowVersionStrategy"/>.
    /// </param>
    /// <param name="canonicalName">
    /// The workflow family's canonical name (for example, <c>"OrderProcessingWorkflow"</c>).
    /// </param>
    /// <param name="optionsName">
    /// An optional options scope name (for example, a named options key) that the factory can use to bind
    /// configuration specific to <paramref name="canonicalName"/>.
    /// </param>
    /// <param name="services">
    /// The application's <see cref="IServiceProvider"/> used to resolve dependencies, named options, configuration
    /// sources, and any other services needed to configure the strategy.
    /// </param>
    /// <returns>
    /// A fully configured <see cref="IWorkflowVersionStrategy"/> instance for the specified workflow family.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="strategyType"/> or <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="strategyType"/> does not implement <see cref="IWorkflowVersionStrategy"/> or cannot
    /// be constructed/configured by the factory.
    /// </exception>
    IWorkflowVersionStrategy Create(Type strategyType, string canonicalName, string? optionsName,
        IServiceProvider services);
}
