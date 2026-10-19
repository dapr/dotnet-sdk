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
/// <remarks>
/// Current generators contribute metadata through <see cref="RegisterGenerated"/>. At startup,
/// contributions from all assemblies are combined before selecting each family's latest version.
/// Explicit application registrations take precedence. Fully qualified concrete names remain valid
/// for replay; simple-name aliases are also retained, but cannot distinguish colliding type names.
/// Legacy registration delegates remain supported and their routes are combined. Rebuild legacy
/// generated assemblies to enable global version selection for their workflow families.
/// </remarks>
public static class WorkflowVersioningRegistry
{
    private static readonly object SyncLock = new();
    private static readonly List<Action<WorkflowRuntimeOptions, IServiceProvider>> Registrations = new();
    private static readonly HashSet<WorkflowRuntimeOptions> AppliedOptions = new(ReferenceEqualityComparer.Instance);
    private static readonly ConditionalWeakTable<WorkflowRuntimeOptions, GeneratedWorkflowCatalog> Catalogs = new();

    /// <summary>
    /// Adds generated workflow contributions to the application's combined version catalog.
    /// Intended for generated code; call before the workflow factory is created.
    /// </summary>
    /// <param name="options">The application's workflow runtime options.</param>
    /// <param name="services">The application's service provider.</param>
    /// <param name="registrations">The concrete workflows discovered in an assembly.</param>
    public static void RegisterGenerated(WorkflowRuntimeOptions options, IServiceProvider services,
        IReadOnlyList<GeneratedWorkflowRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registrations);
        var catalog = Catalogs.GetOrCreateValue(options);
        lock (catalog)
        {
            catalog.Add(registrations);
            if (!catalog.IsApplying)
                catalog.Apply(options, services);
        }
    }

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

        var catalog = Catalogs.GetOrCreateValue(options);
        lock (catalog)
        {
            catalog.IsApplying = true;
            try
            {
                var registrationServices = new RegistrationServices(services, catalog);
                var applied = 0;
                while (true)
                {
                    for (; applied < snapshot.Count; applied++)
                        snapshot[applied](options, registrationServices);
                    lock (SyncLock)
                    {
                        if (Registrations.Count == applied)
                            break;
                        snapshot = new List<Action<WorkflowRuntimeOptions, IServiceProvider>>(Registrations);
                    }
                }
                catalog.Apply(options, services);
            }
            catch
            {
                lock (SyncLock)
                    AppliedOptions.Remove(options);
                throw;
            }
            finally
            {
                catalog.IsApplying = false;
            }
        }
    }

    // Older generators publish replacement snapshots. Capture their contributions without
    // changing the public UpdateRoutes replacement contract or losing newer assemblies.
    private sealed class RegistrationServices(IServiceProvider services, GeneratedWorkflowCatalog catalog)
        : IServiceProvider, IWorkflowRouterRegistry
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IWorkflowRouterRegistry) ? this : services.GetService(serviceType);

        public void UpdateRoutes(IReadOnlyDictionary<string, IReadOnlyList<string>> routes)
        {
            foreach (var (name, versions) in routes)
                catalog.LegacyRoutes[name] = catalog.LegacyRoutes.TryGetValue(name, out var existing)
                    ? existing.Concat(versions).Distinct(StringComparer.Ordinal).ToArray()
                    : versions.ToArray();
        }

        public bool Contains(string workflowName) =>
            services.GetService<IWorkflowRouterRegistry>()?.Contains(workflowName) == true;

        public bool TryResolveLatest(out string workflowName)
        {
            workflowName = string.Empty;
            return services.GetService<IWorkflowRouterRegistry>()?.TryResolveLatest(out workflowName) == true;
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<WorkflowRuntimeOptions>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(WorkflowRuntimeOptions? x, WorkflowRuntimeOptions? y) => ReferenceEquals(x, y);

        public int GetHashCode(WorkflowRuntimeOptions obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
