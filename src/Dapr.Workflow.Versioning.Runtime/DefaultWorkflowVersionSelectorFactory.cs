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
/// Default factory that constructs <see cref="IWorkflowVersionSelector"/> instances using DI and, when supported by
/// the selector, provides scope information for per-family configuration (canonical and options name).
/// </summary>
/// <remarks>
/// The factory uses the following resolution order to build the selector:
/// <list type="number">
///   <item><description>Attempt to resolve the exact selectorType from DI.</description></item>
///   <item><description>Fallback to <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/> so constructor dependencies are injected.</description></item>
///   <item><description>Throw if neither path produces an instance.</description></item>
/// </list>
/// </remarks>
public sealed class DefaultWorkflowVersionSelectorFactory : IWorkflowVersionSelectorFactory
{
    /// <inheritdoc />
    public IWorkflowVersionSelector Create(Type selectorType, string canonicalName, string? optionsName,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(selectorType);
        ArgumentNullException.ThrowIfNull(services);
        
        // Prefer container resolution so any existing registrations (singletons, annotations, etc.) are honored
        var instance = services.GetService(selectorType) as IWorkflowVersionSelector ??
                       ActivatorUtilities.CreateInstance(services, selectorType) as IWorkflowVersionSelector;

        if (instance is null)
            throw new InvalidOperationException($"Could not construct selector of type '{selectorType.FullName}'. " +
                                                $"Ensure it implements {nameof(IWorkflowVersionSelector)} and is " +
                                                "resolvable via DI or has an injectable constructor.");

        return instance;
    }
}
