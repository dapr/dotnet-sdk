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
/// Default factory that builds strategies via DI and supports named options binding through the resolved services.
/// </summary>
public sealed class DefaultWorkflowVersionStrategyFactory : IWorkflowVersionStrategyFactory
{
    /// <inheritdoc />
    public IWorkflowVersionStrategy Create(
        Type strategyType, 
        string canonicalName, 
        string? optionsName,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        ArgumentNullException.ThrowIfNull(services);
        
        if (!typeof(IWorkflowVersionStrategy).IsAssignableFrom(strategyType))
            throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' must implement '{typeof(IWorkflowVersionStrategy).FullName}'.");

        // Prefer DI/ActivatorUtilities so constructor injection works.
        var obj = services.GetService(strategyType) ??
                       ActivatorUtilities.CreateInstance(services, strategyType);

        if (obj is not IWorkflowVersionStrategy instance)
            throw new InvalidOperationException(
                $"Could not construct strategy of type '{strategyType.FullName}'.");

        return instance;
    }
}
