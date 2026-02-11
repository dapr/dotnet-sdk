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

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Creates configured <see cref="IWorkflowVersionSelector"/> instances that apply selection
/// policy to choose the "latest" workflow version within a canonical family.
/// </summary>
/// <remarks>
/// <para>
/// The selector is separate from the strategy to allow policies that go beyond simple
/// string comparison—such as excluding pre-release versions, honoring branch rules, or
/// implementing canary selection.
/// </para>
/// </remarks>
public interface IWorkflowVersionSelectorFactory
{
    /// <summary>
    /// Creates a configured <see cref="IWorkflowVersionSelector"/> for the given workflow family.
    /// </summary>
    /// <param name="selectorType">
    /// The concrete selector type to instantiate. Must implement <see cref="IWorkflowVersionSelector"/>.
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
    /// sources, and any other services needed to configure the selector.
    /// </param>
    /// <returns>
    /// A fully configured <see cref="IWorkflowVersionSelector"/> instance for the specified workflow family.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="selectorType"/> or <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="selectorType"/> does not implement <see cref="IWorkflowVersionSelector"/> or canno
    /// be constructed/configured by the factory.
    /// </exception>
    IWorkflowVersionSelector Create(Type selectorType, string canonicalName, string? optionsName,
        IServiceProvider services);
}
