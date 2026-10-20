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

using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Dapr.Workflow.Versioning;

internal sealed class GeneratedWorkflowCatalog
{
    private readonly Dictionary<Type, GeneratedWorkflowRegistration> _entries = [];
    internal Dictionary<string, IReadOnlyList<string>> LegacyRoutes { get; } = new(StringComparer.Ordinal);
    internal bool IsApplying { get; set; }

    internal void Add(IReadOnlyList<GeneratedWorkflowRegistration> entries)
    {
        foreach (var entry in entries)
        {
            // A referenced module can be loaded by typeof without its initializer having
            // run yet. Its own generator may know additional internal workflows.
            RuntimeHelpers.RunModuleConstructor(entry.WorkflowType.Module.ModuleHandle);
            _entries[entry.WorkflowType] = entry;
        }
    }

    internal void Apply(WorkflowRuntimeOptions options, IServiceProvider services)
    {
        var routes = new Dictionary<string, IReadOnlyList<string>>(LegacyRoutes, StringComparer.Ordinal);
        if (_entries.Count != 0)
        {
            var settings = services.GetRequiredService<WorkflowVersioningOptions>();
            var diagnostics = services.GetService<IWorkflowVersionDiagnostics>() ?? new DefaultWorkflowVersioningDiagnostics();
            var factory = services.GetService<IWorkflowVersionStrategyFactory>();
            var defaultStrategy = settings.DefaultStrategy?.Invoke(services)
                ?? throw new InvalidOperationException("No default workflow versioning strategy configured.");
            var selector = settings.DefaultSelector?.Invoke(services) ?? new MaxVersionSelector();
            var families = new Dictionary<string, List<(string Version, GeneratedWorkflowRegistration Entry)>>(StringComparer.Ordinal);

            // A stable order also makes simple-name collisions independent of assembly load order.
            foreach (var entry in _entries.Values.OrderBy(e => e.WorkflowTypeName, StringComparer.Ordinal)
                         .ThenBy(e => e.WorkflowType.Assembly.FullName, StringComparer.Ordinal))
            {
                var canonical = entry.CanonicalName;
                var version = entry.Version;
                var strategy = entry.StrategyType is null ? defaultStrategy :
                    factory?.Create(entry.StrategyType, canonical ?? entry.WorkflowType.Name, entry.OptionsName, services)
                    ?? throw new InvalidOperationException(diagnostics.UnknownStrategyMessage(entry.WorkflowTypeName, entry.StrategyType));
                if (string.IsNullOrEmpty(canonical) || string.IsNullOrEmpty(version))
                {
                    if (!strategy.TryParse(entry.WorkflowType.Name, out var parsedCanonical, out var parsedVersion))
                        throw new InvalidOperationException(diagnostics.CouldNotParseMessage(entry.WorkflowTypeName));
                    canonical = string.IsNullOrEmpty(canonical) ? parsedCanonical : canonical;
                    version = string.IsNullOrEmpty(version) ? parsedVersion : version;
                }

                if (!families.TryGetValue(canonical!, out var family))
                    families[canonical!] = family = [];
                family.Add((version!, entry));
            }

            var registrations = new Dictionary<string, Action<WorkflowRuntimeOptions, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _entries.Values.OrderBy(e => e.WorkflowTypeName, StringComparer.Ordinal)
                         .ThenBy(e => e.WorkflowType.Assembly.FullName, StringComparer.Ordinal))
            {
                registrations.TryAdd(entry.WorkflowType.Name, entry.Register);
                registrations[entry.WorkflowTypeName] = entry.Register;
            }

            foreach (var (canonical, family) in families)
            {
                var overrides = family.Select(v => v.Entry).Where(e => e.StrategyType is not null)
                    .Select(e => (e.StrategyType, e.OptionsName)).Distinct().ToList();
                if (overrides.Count > 1)
                    throw new InvalidOperationException($"Conflicting workflow version strategies or options for family '{canonical}'.");
                var strategy = overrides.Count == 0 ? defaultStrategy :
                    factory!.Create(overrides[0].StrategyType!, canonical, overrides[0].OptionsName, services);
                var versions = family.Select(v => new WorkflowVersionIdentity(canonical, v.Version,
                    v.Entry.WorkflowTypeName, v.Entry.WorkflowType.Assembly.GetName().Name)).ToList();
                WorkflowVersionIdentity latest;
                try
                {
                    latest = selector.SelectLatest(canonical, versions, strategy);
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidOperationException(diagnostics.AmbiguousLatestMessage(canonical,
                        versions.GroupBy(v => v.Version).OrderByDescending(g => g.Key, strategy)
                            .First().Select(v => v.Version).ToList()));
                }

                var selected = family.SingleOrDefault(v => v.Entry.WorkflowTypeName == latest.TypeName &&
                    (latest.AssemblyName is null || v.Entry.WorkflowType.Assembly.GetName().Name == latest.AssemblyName)).Entry;
                if (selected is null)
                    throw new InvalidOperationException($"The workflow selector returned an unknown candidate for '{canonical}'.");
                registrations[canonical] = selected.Register;
                var names = versions.OrderByDescending(v => v.Version, strategy).Select(v => v.TypeName).ToList();
                names.Remove(latest.TypeName);
                names.Insert(0, latest.TypeName);
                names.AddRange(family.Select(v => v.Entry.WorkflowType.Name));
                if (routes.TryGetValue(canonical, out var legacy))
                    names.AddRange(legacy);
                routes[canonical] = names.Distinct(StringComparer.Ordinal).ToArray();
            }

            foreach (var (name, register) in registrations)
                options.SetGeneratedWorkflow(name, register);
        }

        // Explicit aliases are valid worker/replay names, but not additional version families.
        if (routes.Count > 0)
        {
            var first = routes.Keys.First();
            routes[first] = routes[first].Concat(options.RegisteredWorkflowNames).Distinct(StringComparer.Ordinal).ToArray();
        }
        else
        {
            foreach (var name in options.RegisteredWorkflowNames)
                routes[name] = new[] { name };
        }
        services.GetService<IWorkflowRouterRegistry>()?.UpdateRoutes(routes);
    }
}
