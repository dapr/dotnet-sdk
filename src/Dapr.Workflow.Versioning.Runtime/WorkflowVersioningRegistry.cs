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

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Static registration point for source-generated workflow registries.
/// </summary>
public static class WorkflowVersioningRegistry
{
    private static readonly object SyncLock = new();
    private static readonly List<Action<WorkflowRuntimeOptions, IServiceProvider>> Registrations = new();
    private static readonly HashSet<WorkflowRuntimeOptions> AppliedOptions = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Registers a source-generated workflow registry.
    /// </summary>
    /// <param name="registration">An action injecting a <see cref="WorkflowRuntimeOptions"/> and <see cref="IServiceProvider"/>.</param>
    public static void Register(Action<WorkflowRuntimeOptions, IServiceProvider> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (SyncLock)
        {
            if (!Registrations.Contains(registration))
            {
                Registrations.Add(registration);
            }
        }
    }

    internal static void Apply(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.GetService<WorkflowRuntimeOptions>();
        if (options is null)
            return;

        List<Action<WorkflowRuntimeOptions, IServiceProvider>> snapshot;

        lock (SyncLock)
        {
            if (!AppliedOptions.Add(options))
            {
                return;
            }

            snapshot = new List<Action<WorkflowRuntimeOptions, IServiceProvider>>(Registrations);
        }

        foreach (var s in snapshot)
        {
            s(options, services);
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<WorkflowRuntimeOptions>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(WorkflowRuntimeOptions? x, WorkflowRuntimeOptions? y) => ReferenceEquals(x, y);

        public int GetHashCode(WorkflowRuntimeOptions obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
