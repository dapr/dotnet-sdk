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

namespace Dapr.Workflow;

/// <summary>
/// Static registration point for source-generated workflow and activity auto-registrations.
/// The Dapr Workflow source generator emits a <c>[ModuleInitializer]</c> that calls
/// <see cref="Register"/> at process startup. <c>AddDaprWorkflow()</c> then calls
/// <see cref="Apply"/> to push all discovered types into <see cref="WorkflowRuntimeOptions"/>
/// before the workflow factory is built — meaning application code never has to call
/// <c>RegisterWorkflow</c> or <c>RegisterActivity</c> manually.
/// </summary>
/// <remarks>
/// Registrations stored here use first-write-wins semantics (via
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}.TryAdd"/>
/// inside <c>WorkflowsFactory</c>), so any explicit registrations made by
/// the application in its <c>AddDaprWorkflow(configure)</c> callback take precedence over
/// the auto-discovered ones without conflict.
/// </remarks>
public static class WorkflowAutoRegistry
{
    private static readonly object SyncLock = new();
    private static readonly List<Action<WorkflowRuntimeOptions>> Registrations = [];
    private static readonly HashSet<WorkflowRuntimeOptions> AppliedOptions = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Registers a source-generated auto-registration callback.
    /// Called from <c>[ModuleInitializer]</c> code emitted by the Dapr Workflow source generator.
    /// Safe to call from multiple assemblies; duplicate delegates are silently ignored.
    /// </summary>
    /// <param name="registration">
    /// An action that registers discovered workflows and activities with the provided
    /// <see cref="WorkflowRuntimeOptions"/>.
    /// </param>
    public static void Register(Action<WorkflowRuntimeOptions> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (SyncLock)
        {
            if (!Registrations.Contains(registration))
                Registrations.Add(registration);
        }
    }

    /// <summary>
    /// Applies all source-generated auto-registrations to <paramref name="options"/>.
    /// Called once per <see cref="WorkflowRuntimeOptions"/> instance (idempotent on repeated calls).
    /// </summary>
    internal static void Apply(WorkflowRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<Action<WorkflowRuntimeOptions>> snapshot;

        lock (SyncLock)
        {
            // Guard against multiple AddDaprWorkflow() calls that share the same options instance.
            if (!AppliedOptions.Add(options))
                return;

            snapshot = [.. Registrations];
        }

        foreach (var registration in snapshot)
            registration(options);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<WorkflowRuntimeOptions>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(WorkflowRuntimeOptions? x, WorkflowRuntimeOptions? y) => ReferenceEquals(x, y);
        public int GetHashCode(WorkflowRuntimeOptions obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
