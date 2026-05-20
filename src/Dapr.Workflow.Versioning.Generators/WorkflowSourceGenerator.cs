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
/// Incremental source generator that discovers Dapr Workflow and Activity types, reads optional
/// versioning metadata, and emits a registry that:
/// <list type="bullet">
///   <item>registers all workflow and activity implementations via
///         <c>WorkflowAutoRegistry</c> (simple, no-config path for plain <c>AddDaprWorkflow()</c>), and</item>
///   <item>produces a canonical-name registry ordered by version using the configured strategy
///         and selector (versioning path via <c>WorkflowVersioningRegistry</c>).</item>
/// </list>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class WorkflowSourceGenerator : IIncrementalGenerator
{
    private const string WorkflowBaseMetadataName = "Dapr.Workflow.Workflow`2";
    private const string ActivityBaseMetadataName = "Dapr.Workflow.WorkflowActivity`2";
    private const string WorkflowVersionAttributeFullName = "Dapr.Workflow.Versioning.WorkflowVersionAttribute";
    private const string ScanReferencesPropertyName = "build_property.DaprWorkflowVersioningScanReferences";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue(ScanReferencesPropertyName, out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        // Cache the known symbols
        var known = context.CompilationProvider.Select((c, _) =>
            new KnownSymbols(
                WorkflowBase: c.GetTypeByMetadataName(WorkflowBaseMetadataName),
                WorkflowVersionAttribute: c.GetTypeByMetadataName(WorkflowVersionAttributeFullName),
                ActivityBase: c.GetTypeByMetadataName(ActivityBaseMetadataName)));

        // Report diagnostic about base type resolution
        context.RegisterSourceOutput(known, (spc, ks) =>
        {
            if (ks.WorkflowBase is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPRWFVER001",
                        "Workflow base type not found",
                        $"The source generator could not find the type '{WorkflowBaseMetadataName}'. Ensure that Dapr.Workflow.Abstractions is properly referenced.",
                        "Dapr.Workflow.Versioning",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None));
            }
            else
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPRWFVER003",
                        "Workflow base type found",
                        $"Source generator successfully found workflow base type '{WorkflowBaseMetadataName}'",
                        "Dapr.Workflow.Versioning",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true),
                    Location.None));
            }
        });

        // Discover candidate class symbols (shared between workflow and activity pipelines)
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
            static (ctx, _) =>
                (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node));

        // Count candidates for diagnostics
        var candidatesWithCount = candidates.Collect();
        context.RegisterSourceOutput(candidatesWithCount, (spc, candidateArray) =>
        {
            var nonNullCount = candidateArray.Count(x => x is not null);
            spc.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DAPRWFVER004",
                    "Candidate classes found",
                    $"Source generator found {nonNullCount} candidate class(es) with base lists (out of {candidateArray.Length} total)",
                    "Dapr.Workflow.Versioning",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true),
                Location.None));
        });

        // ── Workflow discovery pipeline ──────────────────────────────────────────

        var inputs = candidates.Combine(known);

        // Filter and transform with proper symbol equality checks
        var discoveredWithDiagnostics = inputs
            .Select((pair, _) =>
            {
                var (symbol, ks) = pair;
                if (symbol is null)
                    return (Workflow: (DiscoveredWorkflow?)null, Diagnostic: (string?)null);

                var symbolName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Check derives from Dapr.Workflow.Workflow<,>
                if (!InheritsFromWorkflow(symbol, ks.WorkflowBase))
                {
                    var baseTypeInfo = symbol.BaseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "null";
                    return (Workflow: (DiscoveredWorkflow?)null,
                        Diagnostic: (string?)$"Rejected '{symbolName}': does not inherit from Workflow<,> (base type: {baseTypeInfo})");
                }

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

                var workflow = BuildDiscoveredWorkflow(symbol, attrData);
                return (Workflow: (DiscoveredWorkflow?)workflow, Diagnostic: (string?)$"Discovered workflow: '{symbolName}'");
            });

        var discovered = discoveredWithDiagnostics
            .Select((item, _) => item.Workflow)
            .Where(x => x is not null);

        // Report diagnostics about workflow filtering
        context.RegisterSourceOutput(discoveredWithDiagnostics.Collect(), (spc, items) =>
        {
            foreach (var item in items)
            {
                if (item.Diagnostic is not null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DAPRWFVER005",
                            "Workflow filtering",
                            item.Diagnostic,
                            "Dapr.Workflow.Versioning",
                            DiagnosticSeverity.Info,
                            isEnabledByDefault: true),
                        Location.None));
                }
            }
        });

        var referencedWorkflows = context.CompilationProvider
            .Combine(known)
            .Combine(scanReferences)
            .Select((input, _) =>
            {
                var ((compilation, ks), scan) = input;
                if (!scan || ks.WorkflowBase is null)
                    return ImmutableArray<DiscoveredWorkflow?>.Empty;

                var list = new List<DiscoveredWorkflow?>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                    list.AddRange(DiscoverReferencedWorkflows(assembly, ks, compilation.Assembly));

                return list.ToImmutableArray();
            });

        var discoveredAllWorkflows = discovered.Collect()
            .Combine(referencedWorkflows)
            .Select((input, _) =>
            {
                var (current, extra) = input;
                if (extra.IsDefaultOrEmpty)
                    return current;

                var list = new List<DiscoveredWorkflow?>(current.Length + extra.Length);
                list.AddRange(current);
                list.AddRange(extra);
                return list.ToImmutableArray();
            });

        // ── Activity discovery pipeline ──────────────────────────────────────────

        var discoveredActivities = inputs
            .Select((pair, _) =>
            {
                var (symbol, ks) = pair;
                // Abstract or open-generic types cannot be instantiated; skip them.
                if (symbol is null || symbol.IsAbstract || symbol.TypeParameters.Length > 0)
                    return (DiscoveredActivity?)null;

                if (!InheritsFromActivity(symbol, ks.ActivityBase))
                    return null;

                return new DiscoveredActivity(
                    ActivityTypeName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    ActivitySimpleName: symbol.Name);
            })
            .Where(x => x is not null);

        var referencedActivities = context.CompilationProvider
            .Combine(known)
            .Combine(scanReferences)
            .Select((input, _) =>
            {
                var ((compilation, ks), scan) = input;
                if (!scan || ks.ActivityBase is null)
                    return ImmutableArray<DiscoveredActivity?>.Empty;

                var list = new List<DiscoveredActivity?>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                    list.AddRange(DiscoverReferencedActivities(assembly, ks, compilation.Assembly));

                return list.ToImmutableArray();
            });

        var discoveredAllActivities = discoveredActivities.Collect()
            .Combine(referencedActivities)
            .Select((input, _) =>
            {
                var (current, extra) = input;
                if (extra.IsDefaultOrEmpty)
                    return current;

                var list = new List<DiscoveredActivity?>(current.Length + extra.Length);
                list.AddRange(current);
                list.AddRange(extra);
                return list.ToImmutableArray();
            });

        // ── Combined emission ────────────────────────────────────────────────────

        var allDiscovered = discoveredAllWorkflows.Combine(discoveredAllActivities);

        context.RegisterSourceOutput(allDiscovered, (spc, items) =>
        {
            var (workflowItems, activityItems) = items;
            var workflows = workflowItems.Where(x => x is not null).ToList();
            var activities = activityItems.Where(x => x is not null).ToList();

            if (workflows.Count > 0 || activities.Count > 0)
            {
                var allNames = string.Join(", ",
                    workflows.Select(w => w!.WorkflowTypeName.Split('.').Last())
                    .Concat(activities.Select(a => a!.ActivityTypeName.Split('.').Last())));

                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPRWFVER008",
                        "Workflow auto-registration active",
                        $"Dapr Workflow source generator: discovered {workflows.Count} workflow(s) and {activities.Count} activity/activities: {allNames}. Build with -v:n to see this message.",
                        "Dapr.Workflow.Versioning",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true,
                        helpLinkUri: "https://docs.dapr.io/developing-applications/building-blocks/workflow/"),
                    Location.None));
            }

            spc.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DAPRWFVER002",
                    "Final type count",
                    $"Source generator will generate registry for {workflows.Count} workflow(s) and {activities.Count} activity/activities. To view generated code, add <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles> to your project file.",
                    "Dapr.Workflow.Versioning",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true),
                Location.None));

            if (workflows.Count > 0)
            {
                foreach (var wf in workflows)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DAPRWFVER006",
                            "Generating for workflow",
                            $"Generating registration code for workflow: {wf!.WorkflowTypeName}",
                            "Dapr.Workflow.Versioning",
                            DiagnosticSeverity.Info,
                            isEnabledByDefault: true),
                        Location.None));
                }

                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPRWFVER007",
                        "Generated file info",
                        "Generated 'Dapr_Workflow_Versioning.g.cs' with workflow and activity registration code. Set EmitCompilerGeneratedFiles=true in your project to write generated files to disk at obj/$(Configuration)/$(TargetFramework)/generated/",
                        "Dapr.Workflow.Versioning",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true),
                    Location.None));
            }

            EmitRegistry(spc, workflowItems, activityItems);
        });
    }

    // ── Inheritance helpers ─────────────────────────────────────────────────────

    private static bool InheritsFromWorkflow(INamedTypeSymbol symbol, INamedTypeSymbol? workflowBase)
    {
        if (workflowBase is null) return false;

        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            var od = t.OriginalDefinition;
            if (od is INamedTypeSymbol &&
                SymbolEqualityComparer.Default.Equals(od, workflowBase))
                return true;
        }

        return false;
    }

    private static bool InheritsFromActivity(INamedTypeSymbol symbol, INamedTypeSymbol? activityBase)
    {
        if (activityBase is null) return false;

        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            var od = t.OriginalDefinition;
            if (od is INamedTypeSymbol &&
                SymbolEqualityComparer.Default.Equals(od, activityBase))
                return true;
        }

        return false;
    }

    // ── Discovery builders ──────────────────────────────────────────────────────

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
                switch (kvp.Key)
                {
                    case "CanonicalName":
                        canonical = kvp.Value.Value?.ToString();
                        break;
                    case "Version":
                        version = kvp.Value.Value?.ToString();
                        break;
                    case "OptionsName":
                        optionsName = kvp.Value.Value?.ToString();
                        break;
                    case "StrategyType":
                        if (kvp.Value.Value is INamedTypeSymbol typeSym)
                            strategyTypeName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        break;
                }
            }
        }

        return new DiscoveredWorkflow(
            WorkflowTypeName: workflowSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            WorkflowSimpleName: workflowSymbol.Name,
            IsAbstractOrGeneric: workflowSymbol.IsAbstract || workflowSymbol.TypeParameters.Length > 0,
            DeclaredCanonicalName: string.IsNullOrWhiteSpace(canonical) ? null : canonical,
            DeclaredVersion: string.IsNullOrWhiteSpace(version) ? null : version,
            StrategyTypeName: strategyTypeName,
            OptionsName: string.IsNullOrWhiteSpace(optionsName) ? null : optionsName
        );
    }

    // ── Referenced-assembly scanning ────────────────────────────────────────────

    private static IEnumerable<DiscoveredWorkflow> DiscoverReferencedWorkflows(
        IAssemblySymbol assemblySymbol,
        KnownSymbols knownSymbols,
        IAssemblySymbol currentAssembly)
    {
        foreach (var type in EnumerateTypes(assemblySymbol.GlobalNamespace))
        {
            if (!IsAccessibleFromAssembly(type, currentAssembly))
                continue;

            if (!InheritsFromWorkflow(type, knownSymbols.WorkflowBase))
                continue;

            AttributeData? attrData = null;
            if (knownSymbols.WorkflowVersionAttribute is not null)
            {
                attrData = type.GetAttributes()
                    .FirstOrDefault(a =>
                        SymbolEqualityComparer.Default.Equals(a.AttributeClass, knownSymbols.WorkflowVersionAttribute));
            }

            attrData ??= type.GetAttributes().FirstOrDefault(a =>
                string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    $"global::{WorkflowVersionAttributeFullName}", StringComparison.Ordinal));

            yield return BuildDiscoveredWorkflow(type, attrData);
        }
    }

    private static IEnumerable<DiscoveredActivity> DiscoverReferencedActivities(
        IAssemblySymbol assemblySymbol,
        KnownSymbols knownSymbols,
        IAssemblySymbol currentAssembly)
    {
        foreach (var type in EnumerateTypes(assemblySymbol.GlobalNamespace))
        {
            if (!IsAccessibleFromAssembly(type, currentAssembly))
                continue;

            // Skip abstract types and open generics — they can't be instantiated.
            if (type.IsAbstract || type.TypeParameters.Length > 0)
                continue;

            if (!InheritsFromActivity(type, knownSymbols.ActivityBase))
                continue;

            yield return new DiscoveredActivity(
                ActivityTypeName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ActivitySimpleName: type.Name);
        }
    }

    // ── Namespace/type enumeration ──────────────────────────────────────────────

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol root)
    {
        foreach (var member in root.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol ns:
                    foreach (var type in EnumerateTypes(ns))
                        yield return type;
                    break;
                case INamedTypeSymbol type:
                    foreach (var nested in EnumerateNestedTypes(type))
                        yield return nested;
                    break;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var child in EnumerateNestedTypes(nested))
                yield return child;
        }
    }

    // ── Accessibility helpers ───────────────────────────────────────────────────

    private static bool IsAccessibleFromAssembly(INamedTypeSymbol type, IAssemblySymbol currentAssembly)
    {
        for (var containing = type; containing is not null; containing = containing.ContainingType)
        {
            if (!IsAccessibleCore(containing, currentAssembly))
                return false;
        }

        return true;
    }

    private static bool IsAccessibleCore(INamedTypeSymbol type, IAssemblySymbol currentAssembly)
    {
        switch (type.DeclaredAccessibility)
        {
            case Accessibility.Public:
                return true;
            case Accessibility.Internal:
            case Accessibility.ProtectedOrInternal:
                return type.ContainingAssembly.GivesAccessTo(currentAssembly);
            default:
                return false;
        }
    }

    // ── Emission ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Final source-emission step. Adds the generated registry source to the compilation.
    /// </summary>
    private static void EmitRegistry(
        SourceProductionContext context,
        ImmutableArray<DiscoveredWorkflow?> workflowItems,
        ImmutableArray<DiscoveredActivity?> activityItems)
    {
        var workflows = workflowItems.IsDefaultOrEmpty
            ? []
            : workflowItems
                .Where(x => x is not null)
                .Select(x => x!)
                .Distinct(new DiscoveredWorkflowComparer())
                .OrderBy(x => x.WorkflowTypeName, StringComparer.Ordinal)
                .ThenBy(x => x.DeclaredCanonicalName ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.DeclaredVersion ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.StrategyTypeName ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.OptionsName ?? string.Empty, StringComparer.Ordinal)
                .ToList();

        var activities = activityItems.IsDefaultOrEmpty
            ? []
            : activityItems
                .Where(x => x is not null)
                .Select(x => x!)
                .Distinct(new DiscoveredActivityComparer())
                .OrderBy(x => x.ActivityTypeName, StringComparer.Ordinal)
                .ToList();

        if (workflows.Count == 0 && activities.Count == 0)
            return;

        var source = GenerateRegistrySource(workflows, activities);
        context.AddSource("Dapr_Workflow_Versioning.g.cs", source);
    }

    private static string GenerateRegistrySource(
        IReadOnlyList<DiscoveredWorkflow> workflows,
        IReadOnlyList<DiscoveredActivity> activities)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Dapr.Workflow;");
        sb.AppendLine("using Dapr.Workflow.Versioning;");

        sb.AppendLine();
        sb.AppendLine("namespace Dapr.Workflow.Versioning");
        sb.AppendLine("{");

        // ── GeneratedWorkflowVersionRegistry ────────────────────────────────────
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Generated workflow and activity registry that:");
        sb.AppendLine("    /// <list type=\"bullet\">");
        sb.AppendLine("    ///   <item>auto-registers all discovered types via <see cref=\"global::Dapr.Workflow.WorkflowAutoRegistry\"/> (used by <c>AddDaprWorkflow()</c>), and</item>");
        sb.AppendLine("    ///   <item>provides a canonical-name mapping ordered by version for the optional versioning path.</item>");
        sb.AppendLine("    /// </list>");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static partial class GeneratedWorkflowVersionRegistry");
        sb.AppendLine("    {");

        // RegisterAlias helper (versioning path)
        sb.AppendLine("        private static void RegisterAlias(global::Dapr.Workflow.WorkflowRuntimeOptions options, string canonical, string latestName)");
        sb.AppendLine("        {");
        bool firstAlias = true;
        foreach (var wf in workflows)
        {
            var cond = $"string.Equals(latestName, {CodeLiteral(wf.WorkflowTypeName)}, StringComparison.Ordinal)";
            sb.AppendLine(firstAlias
                ? $"            if ({cond}) options.RegisterWorkflow<{wf.WorkflowTypeName}>(canonical);"
                : $"            else if ({cond}) options.RegisterWorkflow<{wf.WorkflowTypeName}>(canonical);");
            firstAlias = false;
        }

        if (workflows.Count > 0)
        {
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                throw new InvalidOperationException($\"No registration method generated for selected type '{latestName}'.\");");
            sb.AppendLine("            }");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Entry struct (versioning path)
        sb.AppendLine("        private readonly struct Entry");
        sb.AppendLine("        {");
        sb.AppendLine("            public readonly Type WorkflowType;");
        sb.AppendLine("            public readonly string WorkflowTypeName;");
        sb.AppendLine("            public readonly string? DeclaredCanonicalName;");
        sb.AppendLine("            public readonly string? DeclaredVersion;");
        sb.AppendLine("            public readonly Type? StrategyType;");
        sb.AppendLine("            public readonly string? OptionsName;");
        sb.AppendLine();
        sb.AppendLine("            public Entry(Type wfType, string wfName, string? canonical, string? version, Type? strategyType, string? optionsName)");
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

        // ── RegisterDiscoveredTypes (simple, no-versioning path) ─────────────────
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all discovered workflow and activity types using their simple (short) type name.");
        sb.AppendLine("        /// Called automatically by <c>AddDaprWorkflow()</c> via <see cref=\"global::Dapr.Workflow.WorkflowAutoRegistry\"/>.");
        sb.AppendLine("        /// Duplicate registrations are silently ignored — explicit user registrations always take precedence.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static void RegisterDiscoveredTypes(global::Dapr.Workflow.WorkflowRuntimeOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options is null) throw new ArgumentNullException(nameof(options));");
        sb.AppendLine();

        // Register concrete (non-abstract, non-generic) workflows by simple name
        var concreteWorkflows = workflows.Where(w => !w.IsAbstractOrGeneric).ToList();
        if (concreteWorkflows.Count > 0)
        {
            sb.AppendLine("            // Discovered workflows (registered by simple type name)");
            foreach (var wf in concreteWorkflows)
            {
                sb.AppendLine($"            options.RegisterWorkflow<{wf.WorkflowTypeName}>();");
            }
        }

        if (activities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("            // Discovered activities (registered by simple type name)");
            foreach (var act in activities)
            {
                sb.AppendLine($"            options.RegisterActivity<{act.ActivityTypeName}>();");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // ── RegisterGeneratedWorkflows (full versioning path) ────────────────────
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all discovered workflow types with the Dapr Workflow runtime and");
        sb.AppendLine("        /// registers canonical-name aliases that route to the selected latest version.");
        sb.AppendLine("        /// Requires workflow versioning to be configured via <c>AddDaprWorkflowVersioning()</c>.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"options\">The workflow runtime options used for registration.</param>");
        sb.AppendLine("        /// <param name=\"services\">Application service provider used to resolve strategy/selector runtime services.</param>");
        sb.AppendLine("        public static void RegisterGeneratedWorkflows(global::Dapr.Workflow.WorkflowRuntimeOptions options, global::System.IServiceProvider services)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options is null) throw new ArgumentNullException(nameof(options));");
        sb.AppendLine("            if (services is null) throw new ArgumentNullException(nameof(services));");
        sb.AppendLine();
        sb.AppendLine("            var entries = CreateEntries();");
        sb.AppendLine();
        sb.AppendLine("            // Register concrete workflow implementations with internal fully-qualified names to avoid collisions.");

        foreach (var wf in workflows)
        {
            sb.AppendLine($"            options.RegisterWorkflow<{wf.WorkflowTypeName}>({CodeLiteral(wf.WorkflowTypeName)});");
        }

        sb.AppendLine();
        sb.AppendLine("            var registry = BuildRegistry(services, entries, out var latestMap);");
        sb.AppendLine("            UpdateRouterRegistry(services, entries, registry);");
        sb.AppendLine("            foreach (var kvp in latestMap)");
        sb.AppendLine("            {");
        sb.AppendLine("                var canonical = kvp.Key;");
        sb.AppendLine("                var latestName = kvp.Value;");
        sb.AppendLine("                RegisterAlias(options, canonical, latestName);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Register simple-name aliases for convenience. Collisions are resolved deterministically");
        sb.AppendLine("            // by generator ordering (first registration wins).");
        var simpleAliasNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var wf in workflows)
        {
            if (simpleAliasNames.Add(wf.WorkflowSimpleName))
            {
                sb.AppendLine($"            options.RegisterWorkflow<{wf.WorkflowTypeName}>({CodeLiteral(wf.WorkflowSimpleName)});");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // GetWorkflowVersionRegistry
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets a mapping of canonical workflow names to ordered workflow names.");
        sb.AppendLine("        /// The latest version (as selected by the configured selector) is first.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">Application service provider used to resolve strategy/selector runtime services.</param>");
        sb.AppendLine("        /// <returns>A read-only mapping of canonical names to ordered workflow names.</returns>");
        sb.AppendLine("        public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetWorkflowVersionRegistry(global::System.IServiceProvider services)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (services is null) throw new ArgumentNullException(nameof(services));");
        sb.AppendLine("            var entries = CreateEntries();");
        sb.AppendLine("            return BuildRegistry(services, entries, out _);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // UpdateRouterRegistry
        sb.AppendLine("        private static void UpdateRouterRegistry(global::System.IServiceProvider services, List<Entry> entries, IReadOnlyDictionary<string, IReadOnlyList<string>> registry)");
        sb.AppendLine("        {");
        sb.AppendLine("            var routerRegistry = services.GetService(typeof(global::Dapr.Workflow.Versioning.IWorkflowRouterRegistry)) as global::Dapr.Workflow.Versioning.IWorkflowRouterRegistry;");
        sb.AppendLine("            if (routerRegistry is null) return;");
        sb.AppendLine();
        sb.AppendLine("            var nameMap = new Dictionary<string, string>(StringComparer.Ordinal);");
        sb.AppendLine("            foreach (var e in entries)");
        sb.AppendLine("                nameMap[e.WorkflowTypeName] = e.WorkflowType.Name;");
        sb.AppendLine();
        sb.AppendLine("            var routes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);");
        sb.AppendLine("            foreach (var kvp in registry)");
        sb.AppendLine("            {");
        sb.AppendLine("                var list = kvp.Value.Select(v => nameMap.TryGetValue(v, out var n) ? n : v).ToList();");
        sb.AppendLine("                routes[kvp.Key] = list;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            routerRegistry.UpdateRoutes(routes);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // CreateEntries
        sb.AppendLine("        private static List<Entry> CreateEntries()");
        sb.AppendLine("        {");
        sb.AppendLine("            return new List<Entry>");
        sb.AppendLine("            {");
        foreach (var wf in workflows)
        {
            var strategyTypeLit = string.IsNullOrWhiteSpace(wf.StrategyTypeName) ? "null" : $"typeof({wf.StrategyTypeName})";
            var canonicalLit = wf.DeclaredCanonicalName is null ? "null" : CodeLiteral(wf.DeclaredCanonicalName);
            var versionLit = wf.DeclaredVersion is null ? "null" : CodeLiteral(wf.DeclaredVersion);
            var optionsLit = wf.OptionsName is null ? "null" : CodeLiteral(wf.OptionsName);
            sb.AppendLine($"                new Entry(typeof({wf.WorkflowTypeName}), {CodeLiteral(wf.WorkflowTypeName)}, {canonicalLit}, {versionLit}, {strategyTypeLit}, {optionsLit}),");
        }
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        // BuildRegistry
        sb.AppendLine("        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildRegistry(global::System.IServiceProvider services, List<Entry> entries, out Dictionary<string, string> latestMap)");
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
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.UnknownStrategyMessage(e.WorkflowTypeName, e.StrategyType));");
        sb.AppendLine("                    parseStrategy = strategyFactory.Create(e.StrategyType, e.DeclaredCanonicalName ?? e.WorkflowType.Name, e.OptionsName, services);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (string.IsNullOrEmpty(canonical) || string.IsNullOrEmpty(version))");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (!parseStrategy.TryParse(e.WorkflowType.Name, out var c, out var v))");
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.CouldNotParseMessage(e.WorkflowTypeName));");
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
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.UnknownStrategyMessage(e.WorkflowTypeName, e.StrategyType));");
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
        sb.AppendLine("                    throw new InvalidOperationException(diagnostics.EmptyFamilyMessage(canonical));");
        sb.AppendLine();
        sb.AppendLine("                var strategy = defaultStrategy;");
        sb.AppendLine("                if (familyStrategyTypes.TryGetValue(canonical, out var strategyType) && strategyType is not null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (strategyFactory is null)");
        sb.AppendLine("                        throw new InvalidOperationException(diagnostics.UnknownStrategyMessage(canonical, strategyType));");
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
        sb.AppendLine("    }"); // end GeneratedWorkflowVersionRegistry

        sb.AppendLine();

        // ── GeneratedWorkflowVersionRegistryModule ───────────────────────────────
        sb.AppendLine("    internal static class GeneratedWorkflowVersionRegistryModule");
        sb.AppendLine("    {");
        sb.AppendLine("        [ModuleInitializer]");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        sb.AppendLine("            // Register with the versioning registry (used when AddDaprWorkflowVersioning() is configured).");
        sb.AppendLine("            global::Dapr.Workflow.Versioning.WorkflowVersioningRegistry.Register(GeneratedWorkflowVersionRegistry.RegisterGeneratedWorkflows);");
        sb.AppendLine();
        sb.AppendLine("            // Register with the auto-registry (used by plain AddDaprWorkflow() — no versioning required).");
        sb.AppendLine("            global::Dapr.Workflow.WorkflowAutoRegistry.Register(GeneratedWorkflowVersionRegistry.RegisterDiscoveredTypes);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── Shared helpers ──────────────────────────────────────────────────────────

    private static string CodeLiteral(string s) => "@\"" + s.Replace("\"", "\"\"") + "\"";

    // ── Data records ────────────────────────────────────────────────────────────

    private sealed record DiscoveredWorkflow(
        string WorkflowTypeName,
        string WorkflowSimpleName,
        bool IsAbstractOrGeneric,
        string? DeclaredCanonicalName,
        string? DeclaredVersion,
        string? StrategyTypeName,
        string? OptionsName
    );

    private sealed record DiscoveredActivity(
        string ActivityTypeName,
        string ActivitySimpleName
    );

    private sealed class DiscoveredWorkflowComparer : IEqualityComparer<DiscoveredWorkflow>
    {
        public bool Equals(DiscoveredWorkflow? x, DiscoveredWorkflow? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return StringComparer.Ordinal.Equals(x.WorkflowTypeName, y.WorkflowTypeName)
                && StringComparer.Ordinal.Equals(x.DeclaredCanonicalName ?? string.Empty, y.DeclaredCanonicalName ?? string.Empty)
                && StringComparer.Ordinal.Equals(x.DeclaredVersion ?? string.Empty, y.DeclaredVersion ?? string.Empty)
                && StringComparer.Ordinal.Equals(x.StrategyTypeName ?? string.Empty, y.StrategyTypeName ?? string.Empty)
                && StringComparer.Ordinal.Equals(x.OptionsName ?? string.Empty, y.OptionsName ?? string.Empty);
        }

        public int GetHashCode(DiscoveredWorkflow obj)
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(obj.WorkflowTypeName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(obj.DeclaredCanonicalName ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(obj.DeclaredVersion ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(obj.StrategyTypeName ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(obj.OptionsName ?? string.Empty);
                return hash;
            }
        }
    }

    private sealed class DiscoveredActivityComparer : IEqualityComparer<DiscoveredActivity>
    {
        public bool Equals(DiscoveredActivity? x, DiscoveredActivity? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return StringComparer.Ordinal.Equals(x.ActivityTypeName, y.ActivityTypeName);
        }

        public int GetHashCode(DiscoveredActivity obj) =>
            StringComparer.Ordinal.GetHashCode(obj.ActivityTypeName);
    }
}
