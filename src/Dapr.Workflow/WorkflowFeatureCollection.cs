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

using System;
using System.Collections.Generic;

namespace Dapr.Workflow;

/// <summary>
/// A per-execution bag of features keyed by interface type. Populated by the workflow
/// runtime before an orchestrator is invoked and accessed through
/// <see cref="IWorkflowFeatureSource"/>.
/// </summary>
internal sealed class WorkflowFeatureCollection : IWorkflowFeatureRegistry
{
    private readonly Dictionary<Type, object> _features = [];

    /// <inheritdoc />
    public void Set<TFeature>(TFeature instance) where TFeature : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _features[typeof(TFeature)] = instance;
    }

    /// <summary>
    /// Retrieves a registered feature instance.
    /// </summary>
    /// <typeparam name="TFeature">The feature interface type.</typeparam>
    /// <returns>The instance, or <see langword="null"/> if non is registered.</returns>
    public TFeature? Get<TFeature>() where TFeature : class =>
        _features.TryGetValue(typeof(TFeature), out var f) ? (TFeature)f : null;
}
