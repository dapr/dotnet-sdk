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

namespace Dapr.Workflow;

/// <summary>
/// Receives feature registrations for a single workflow execution.
/// </summary>
internal interface IWorkflowFeatureRegistry
{
    /// <summary>
    /// Registers or replaces a feature instance for the current execution.
    /// </summary>
    /// <param name="instance">The feature instance.</param>
    /// <typeparam name="TFeature">The feature instance type under which the instance is exposed.</typeparam>
    void Set<TFeature>(TFeature instance) where TFeature : class;
}
