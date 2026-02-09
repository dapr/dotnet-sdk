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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Incremental source generator that discovers Dapr Workflow types, reads optional versioning metadata
/// and emits a registry that:
/// - registers all workflow implementations, and
/// - produces a canonical-name registry ordered by version using the configured strategy and selector.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class WorkflowSourceGenerator : IIncrementalGenerator
{
    private const string WorkflowBaseMetadataName = "Dapr.Workflow.Workflow`2";
    private const string WorkflowVersionAttributeFullName = "Dapr.Workflow.Versioning.WorkflowVersionAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Cache the attribute symbol
        var known = context.CompilationProvider.Select((c, _) =>
            new KnownSymbols(
                WorkflowBase: c.GetTypeByMetadataName(WorkflowBaseMetadataName),
                WorkflowVersionAttribute: c.GetTypeByMetadataName(WorkflowVersionAttributeFullName)));

        // Discover candidate class symbols
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
            static (ctx, _) =>
                (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node));

        // Combine the attribute symbol with each candidate symbol
        var inputs = candidates.Combine(known);

        // Filter and transform with proper symbol equality checks
        var discovered = inputs
            .Select((pair, _) =>
            {
                var (symbol, ks) = pair;
                if (symbol is null)
                    return null;

                // Check derives from Dapr.Workflow.Workflow<,>
                if (!InheritsFromWorkflow(symbol, ks.WorkflowBase))
                    return null;

                // Look for [WorkflowVersion] by symbol identity
                AttributeData? attrData = null;
                if (ks.WorkflowVersionAttribute is not null)
                {
                    attrData = symbol.GetAttributes()
                        .FirstOrDefault(a =>
                            SymbolEqualityComparer.Default.Equals(a.AttributeClass, ks.WorkflowVersionAttribute));
                }

                attrData ??= symbol.GetAttributes().FirstOrDefault(a =>
                    string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        $"global::{WorkflowVersionAttributeFullName}", StringComparison.Ordinal));

                return BuildDiscoveredWorkflow(symbol, attrData);
            })
            .Where(x => x is not null);

        // Collect and emit
        context.RegisterSourceOutput(discovered.Collect(), EmitRegistry);
    }

    private static bool InheritsFromWorkflow(INamedTypeSymbol symbol, INamedTypeSymbol? workflowBase)
    {
        if (workflowBase is null) return false; // Consumer didn’t reference Dapr.Workflow (no Workflows present)

        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            var od = t.OriginalDefinition;
            if (od is INamedTypeSymbol &&
                SymbolEqualityComparer.Default.Equals(od, workflowBase))
                return true;
        }

        return false;
    }

    private static DiscoveredWorkflow BuildDiscoveredWorkflow(
        INamedTypeSymbol workflowSymbol,
        AttributeData? workflowVersionAttribute)
    {
        string? canonical = null;
        string? version = null;
        string? optionsName = null;
        string? strategyTypeName = null;

        if (workflowVersionAttribute is not null)
        {
            foreach (var kvp in workflowVersionAttribute.NamedArguments)
            {
                var name = kvp.Key;
                var tc = kvp.Value;

                switch (name)
                {
                    case "CanonicalName":
                        canonical = tc.Value?.ToString();
                        break;
                    case "Version":
                        version = tc.Value?.ToString();
                        break;
                    case "OptionsName":
                        optionsName = tc.Value?.ToString();
                        break;
                    case "StrategyType":
                        if (tc.Value is INamedTypeSymbol typeSym)
                        {
                            strategyTypeName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }

                        break;
                }
            }
        }

        return new DiscoveredWorkflow(
            WorkflowTypeName: workflowSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            DeclaredCanonicalName: string.IsNullOrWhiteSpace(canonical) ? null : canonical,
            DeclaredVersion: string.IsNullOrWhiteSpace(version) ? null : version,
            StrategyTypeName: strategyTypeName,
            OptionsName: string.IsNullOrWhiteSpace(optionsName) ? null : optionsName
        );
    }

    /// <summary>
    /// Final source-emission step for the generator. Receives the collected set of discovered
    /// workflow descriptors and adds the generated registry/registration source to the compilation.
    /// </summary>
    private static void EmitRegistry(
        SourceProductionContext context,
        ImmutableArray<DiscoveredWorkflow?> discoveredItems)
    {
        // Nothing to emit if we found no workflows.
        if (discoveredItems.IsDefaultOrEmpty)
            return;

        // Remove nulls, de-dupe by fully-qualified type name, and stabilize the order for deterministic output.
        var list = discoveredItems
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(new DiscoveredWorkflowComparer())
            .OrderBy(x => x.WorkflowTypeName, StringComparer.Ordinal)
            .ToList();

        if (list.Count == 0)
            return;

        // Generate the full source and add it to the compilation.
        var source = GenerateRegistrySource(list);
        context.AddSource("Dapr_Workflow_Versioning.g.cs", source);
    }

    private static string GenerateRegistrySource(IReadOnlyList<DiscoveredWorkflow> discovered)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Dapr.Workflow;");
        sb.AppendLine("using Dapr.Workflow.Versioning;");

        sb.AppendLine();
        sb.AppendLine("namespace Dapr.Workflow.Versioning");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Generated workflow registry that registers discovered workflows and");
        sb.AppendLine("    /// provides a canonical-name mapping ordered by version.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static partial class GeneratedWorkflowVersionRegistry");
        sb.AppendLine("    {");
        sb.AppendLine("        private static void RegisterAlias(global::Dapr.Workflow.WorkflowRuntimeOptions options, string canonical, string latestName)");
        sb.AppendLine("        {");
        bool firstAlias = true;
        foreach (var wf in discovered)
        {
            var cond = $"string.Equals(latestName, {CodeLiteral(wf.WorkflowTypeName)}, StringComparison.Ordinal)";
            sb.AppendLine(firstAlias
                ? $"            if ({cond}) options.RegisterWorkflow<{wf.WorkflowTypeName}>(canonical);"
                : $"            else if ({cond}) options.RegisterWorkflow<{wf.WorkflowTypeName}>(canonical);");
            firstAlias = false;
        }
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                throw new InvalidOperationException($\"No registration method generated for selected type '{latestName}'.\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Emit runtime registration entry struct to carry discovery + declared hints.
        sb.AppendLine("        private readonly struct Entry");
        sb.AppendLine("        {");
        sb.AppendLine("            public readonly Type WorkflowType;");
        sb.AppendLine("            public readonly string WorkflowTypeName;");
        sb.AppendLine("            public readonly string? DeclaredCanonicalName;");
        sb.AppendLine("            public readonly string? DeclaredVersion;");
        sb.AppendLine("            public readonly Type? StrategyType;");
        sb.AppendLine("            public readonly string? OptionsName;");
        sb.AppendLine();
        sb.AppendLine(
            "            public Entry(Type wfType, string wfName, string? canonical, string? version, Type? strategyType, string? optionsName)");
        sb.AppendLine("            {");
        sb.AppendLine("                WorkflowType = wfType;");
        sb.AppendLine("                WorkflowTypeName = wfName;");
        sb.AppendLine("                DeclaredCanonicalName = canonical;");
        sb.AppendLine("                DeclaredVersion = version;");
        sb.AppendLine("                StrategyType = strategyType;");
        sb.AppendLine("                OptionsName = optionsName;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Emit public API: RegisterGeneratedWorkflows
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all discovered workflow types with the Dapr Workflow runtime and");
        sb.AppendLine("        /// registers canonical-name aliases that route to the selected latest version.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine(
            "        /// <param name=\"options\">The workflow runtime options used for registration.</param>");
        sb.AppendLine(
            "        /// <param name=\"services\">Application service provider (DI root) used to resolve strategy/selector runtime services.</param>");
        sb.AppendLine(
            "        public static void RegisterGeneratedWorkflows(global::Dapr.Workflow.WorkflowRuntimeOptions options, global::System.IServiceProvider services)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options is null) throw new ArgumentNullException(nameof(options));");
        sb.AppendLine("            if (services is null) throw new ArgumentNullException(nameof(services));");
        sb.AppendLine();

        sb.AppendLine("            var entries = CreateEntries();");
        sb.AppendLine();
        sb.AppendLine("            // Register concrete workflow implementations.");
        foreach (var wf in discovered)
        {
            sb.AppendLine($"            options.RegisterWorkflow<{wf.WorkflowTypeName}>();");
        }
        sb.AppendLine();

        sb.AppendLine("            BuildRegistry(services, entries, out var latestMap);");
        sb.AppendLine("            foreach (var kvp in latestMap)");
        sb.AppendLine("            {");
        sb.AppendLine("                var canonical = kvp.Key;");
        sb.AppendLine("                var latestName = kvp.Value;");
        sb.AppendLine("                RegisterAlias(options, canonical, latestName);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Emit public API: GetWorkflowVersionRegistry
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets a mapping of canonical workflow names to ordered workflow names.");
        sb.AppendLine("        /// The latest version (as selected by the configured selector) is first.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine(
            "        /// <param name=\"services\">Application service provider (DI root) used to resolve strategy/selector runtime services.</param>");
        sb.AppendLine(
            "        /// <returns>A read-only mapping of canonical names to ordered workflow names.</returns>");
        sb.AppendLine(
            "        public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetWorkflowVersionRegistry(global::System.IServiceProvider services)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (services is null) throw new ArgumentNullException(nameof(services));");
        sb.AppendLine("            var entries = CreateEntries();");
        sb.AppendLine("            return BuildRegistry(services, entries, out _);");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        private static List<Entry> CreateEntries()");
        sb.AppendLine("        {");
        sb.AppendLine("            return new List<Entry>");
        sb.AppendLine("            {");
        foreach (var wf in discovered)
        {
            var strategyTypeLit = string.IsNullOrWhiteSpace(wf.StrategyTypeName)
                ? "null"
                : $"typeof({wf.StrategyTypeName})";
            var canonicalLit = wf.DeclaredCanonicalName is null ? "null" : CodeLiteral(wf.DeclaredCanonicalName);
            var versionLit = wf.DeclaredVersion is null ? "null" : CodeLiteral(wf.DeclaredVersion);
            var optionsLit = wf.OptionsName is null ? "null" : CodeLiteral(wf.OptionsName);

            sb.AppendLine(
                $"                new Entry(typeof({wf.WorkflowTypeName}), {CodeLiteral(wf.WorkflowTypeName)}, {canonicalLit}, {versionLit}, {strategyTypeLit}, {optionsLit}),");
        }

        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine(
            "        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildRegistry(global::System.IServiceProvider services, List<Entry> entries, out Dictionary<string, string> latestMap)");
        sb.AppendLine("        {");
        sb.AppendLine("            latestMap = new Dictionary<string, string>(StringComparer.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("            var opts = services.GetService(typeof(global::Dapr.Workflow.Versioning.WorkflowVersioningOptions)) as global::Dapr.Workflow.Versioning.WorkflowVersioningOptions");
        sb.AppendLine("                ?? throw new InvalidOperationException(\"WorkflowVersioningOptions is not registered.\");");
        sb.AppendLine("            var diagnostics = services.GetService(typeof(global::Dapr.Workflow.Versioning.IWorkflowVersionDiagnostics)) as global::Dapr.Workflow.Versioning.IWorkflowVersionDiagnostics");
        sb.AppendLine("                ?? new global::Dapr.Workflow.Versioning.DefaultWorkflowVersioningDiagnostics();");
        sb.AppendLine();
        sb.AppendLine("            var strategyFactory = services.GetService(typeof(global::Dapr.Workflow.Versioning.IWorkflowVersionStrategyFactory)) as global::Dapr.Workflow.Versioning.IWorkflowVersionStrategyFactory;");
        sb.AppendLine("            var defaultStrategy = opts.DefaultStrategy?.Invoke(services)");
        sb.AppendLine("                ?? throw new InvalidOperationException(\"No default workflow versioning strategy configured.\");");
        sb.AppendLine("            var selector = opts.DefaultSelector?.Invoke(services) ?? new global::Dapr.Workflow.Versioning.MaxVersionSelector();");
        sb.AppendLine();
        sb.AppendLine("            var families = new Dictionary<string, List<(string Version, Entry Entry)>>(StringComparer.Ordinal);");
        sb.AppendLine("            var familyStrategyTypes = new Dictionary<string, Type?>(StringComparer.Ordinal);");
        sb.AppendLine("            var familyOptionsNames = new Dictionary<string, string?>(StringComparer.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var e in entries)");
        sb.AppendLine("            {");
        sb.AppendLine("                string canonical = e.DeclaredCanonicalName ?? string.Empty;");
        sb.AppendLine("                string version = e.DeclaredVersion ?? string.Empty;");
        sb.AppendLine();
        sb.AppendLine("                var parseStrategy = defaultStrategy;");
        sb.AppendLine("                if (e.StrategyType is not null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (strategyFactory is null)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.UnknownStrategyMessage(e.WorkflowTypeName, e.StrategyType));");
        sb.AppendLine("                    }");
        sb.AppendLine("                    parseStrategy = strategyFactory.Create(e.StrategyType, e.DeclaredCanonicalName ?? e.WorkflowType.Name, e.OptionsName, services);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (string.IsNullOrEmpty(canonical) || string.IsNullOrEmpty(version))");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (!parseStrategy.TryParse(e.WorkflowType.Name, out var c, out var v))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.CouldNotParseMessage(e.WorkflowTypeName));");
        sb.AppendLine("                    }");
        sb.AppendLine("                    canonical = string.IsNullOrEmpty(canonical) ? c : canonical;");
        sb.AppendLine("                    version = string.IsNullOrEmpty(version) ? v : version;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (!families.TryGetValue(canonical, out var list))");
        sb.AppendLine("                {");
        sb.AppendLine("                    list = new List<(string, Entry)>();");
        sb.AppendLine("                    families[canonical] = list;");
        sb.AppendLine("                }");
        sb.AppendLine("                list.Add((version, e));");
        sb.AppendLine();
        sb.AppendLine("                if (e.StrategyType is not null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (familyStrategyTypes.TryGetValue(canonical, out var existing) && existing != e.StrategyType)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.UnknownStrategyMessage(e.WorkflowTypeName, e.StrategyType));");
        sb.AppendLine("                    }");
        sb.AppendLine("                    familyStrategyTypes[canonical] = e.StrategyType;");
        sb.AppendLine("                    familyOptionsNames[canonical] = e.OptionsName;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var registry = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var kvp in families)");
        sb.AppendLine("            {");
        sb.AppendLine("                var canonical = kvp.Key;");
        sb.AppendLine("                var list = kvp.Value;");
        sb.AppendLine("                if (list.Count == 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    throw new InvalidOperationException(diagnostics.EmptyFamilyMessage(canonical));");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                var strategy = defaultStrategy;");
        sb.AppendLine("                if (familyStrategyTypes.TryGetValue(canonical, out var strategyType) && strategyType is not null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (strategyFactory is null)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.UnknownStrategyMessage(canonical, strategyType));");
        sb.AppendLine("                    }");
        sb.AppendLine("                    familyOptionsNames.TryGetValue(canonical, out var optionsName);");
        sb.AppendLine("                    strategy = strategyFactory.Create(strategyType, canonical, optionsName, services);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                var versions = list.Select(v => new global::Dapr.Workflow.Versioning.WorkflowVersionIdentity(canonical, v.Version, v.Entry.WorkflowTypeName, v.Entry.WorkflowType.Assembly.GetName().Name)).ToList();");
        sb.AppendLine("                global::Dapr.Workflow.Versioning.WorkflowVersionIdentity latest;");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    latest = selector.SelectLatest(canonical, versions, strategy);");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (InvalidOperationException)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var tied = versions");
        sb.AppendLine("                        .GroupBy(v => v.Version)");
        sb.AppendLine("                        .OrderByDescending(g => g.Key, strategy)");
        sb.AppendLine("                        .FirstOrDefault();");
        sb.AppendLine("                    throw new InvalidOperationException(diagnostics.AmbiguousLatestMessage(canonical, tied?.Select(v => v.Version).ToList() ?? new List<string>()));");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                var ordered = versions");
        sb.AppendLine("                    .OrderByDescending(v => v.Version, strategy)");
        sb.AppendLine("                    .Select(v => v.TypeName)");
        sb.AppendLine("                    .ToList();");
        sb.AppendLine("                if (ordered.Count != 0 && !string.Equals(ordered[0], latest.TypeName, StringComparison.Ordinal))");
        sb.AppendLine("                {");
        sb.AppendLine("                    ordered.RemoveAll(v => string.Equals(v, latest.TypeName, StringComparison.Ordinal));");
        sb.AppendLine("                    ordered.Insert(0, latest.TypeName);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                registry[canonical] = ordered;");
        sb.AppendLine("                latestMap[canonical] = latest.TypeName;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return registry;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string CodeLiteral(string s) => "@\"" + s.Replace("\"", "\"\"") + "\"";

    private sealed record DiscoveredWorkflow(
        string WorkflowTypeName,
        string? DeclaredCanonicalName,
        string? DeclaredVersion,
        string? StrategyTypeName,
        string? OptionsName
    );

    private sealed class DiscoveredWorkflowComparer : IEqualityComparer<DiscoveredWorkflow>
    {
        public bool Equals(DiscoveredWorkflow? x, DiscoveredWorkflow? y)
            => StringComparer.Ordinal.Equals(x?.WorkflowTypeName, y?.WorkflowTypeName);

        public int GetHashCode(DiscoveredWorkflow obj)
            => StringComparer.Ordinal.GetHashCode(obj.WorkflowTypeName);
    }
}
