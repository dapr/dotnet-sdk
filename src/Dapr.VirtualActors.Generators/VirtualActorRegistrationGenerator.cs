// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.VirtualActors.Generators;

/// <summary>
/// Incremental source generator that discovers all <c>VirtualActor</c> subclasses at compile time
/// and generates AOT-safe registration code including factory delegates and DI wiring.
/// </summary>
/// <remarks>
/// <para>
/// By default, only the current assembly is scanned. Set the MSBuild property
/// <c>DaprVirtualActorsScanReferences</c> to <c>true</c> to also scan referenced assemblies:
/// </para>
/// <code>
/// &lt;PropertyGroup&gt;
///   &lt;DaprVirtualActorsScanReferences&gt;true&lt;/DaprVirtualActorsScanReferences&gt;
/// &lt;/PropertyGroup&gt;
/// </code>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class VirtualActorRegistrationGenerator : IIncrementalGenerator
{
    private const string VirtualActorBaseMetadataName = "Dapr.VirtualActors.Runtime.VirtualActor";
    private const string VirtualActorHostMetadataName = "Dapr.VirtualActors.Runtime.VirtualActorHost";
    private const string IVirtualActorMetadataName = "Dapr.VirtualActors.IVirtualActor";
    private const string ScanReferencesPropertyName = "build_property.DaprVirtualActorsScanReferences";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Determine if we should scan referenced assemblies
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue(ScanReferencesPropertyName, out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        // Cache known symbols
        var known = context.CompilationProvider.Select((c, _) =>
            new KnownSymbols(
                VirtualActorBase: c.GetTypeByMetadataName(VirtualActorBaseMetadataName),
                VirtualActorHost: c.GetTypeByMetadataName(VirtualActorHostMetadataName),
                IVirtualActor: c.GetTypeByMetadataName(IVirtualActorMetadataName)));

        // Report diagnostic about base type resolution
        context.RegisterSourceOutput(known, static (spc, ks) =>
        {
            if (ks.VirtualActorBase is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.VirtualActorBaseTypeNotFound,
                    Location.None,
                    VirtualActorBaseMetadataName));
            }
        });

        // Discover candidate class symbols in the current assembly
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
            static (ctx, _) =>
                (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node));

        // Filter to VirtualActor subclasses
        var localActors = candidates
            .Combine(known)
            .Select(static (pair, _) =>
            {
                var (symbol, ks) = pair;
                if (symbol is null || symbol.IsAbstract || ks.VirtualActorBase is null)
                    return (DiscoveredActor?)null;

                if (!InheritsFrom(symbol, ks.VirtualActorBase))
                    return null;

                return BuildDiscoveredActor(symbol, ks.IVirtualActor);
            })
            .Where(static x => x is not null);

        // Discover actors from referenced assemblies (opt-in)
        var referencedActors = context.CompilationProvider
            .Combine(known)
            .Combine(scanReferences)
            .Select(static (input, _) =>
            {
                var ((compilation, ks), scan) = input;
                if (!scan || ks.VirtualActorBase is null)
                    return ImmutableArray<DiscoveredActor?>.Empty;

                var list = new List<DiscoveredActor?>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    foreach (var type in EnumerateTypes(assembly.GlobalNamespace))
                    {
                        if (type.IsAbstract)
                            continue;
                        if (!IsAccessibleFromAssembly(type, compilation.Assembly))
                            continue;
                        if (!InheritsFrom(type, ks.VirtualActorBase))
                            continue;

                        list.Add(BuildDiscoveredActor(type, ks.IVirtualActor));
                    }
                }

                return list.ToImmutableArray();
            });

        // Combine local and referenced actors
        var allActors = localActors.Collect()
            .Combine(referencedActors)
            .Select(static (input, _) =>
            {
                var (local, referenced) = input;
                if (referenced.IsDefaultOrEmpty)
                    return local;

                var list = new List<DiscoveredActor?>(local.Length + referenced.Length);
                list.AddRange(local);
                list.AddRange(referenced);
                return list.ToImmutableArray();
            });

        // Emit the generated registration code
        context.RegisterSourceOutput(allActors, static (spc, items) =>
        {
            var actors = items.Where(x => x is not null).Select(x => x!).ToList();

            // Deduplicate by fully qualified type name
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var unique = new List<DiscoveredActor>();
            foreach (var actor in actors)
            {
                if (seen.Add(actor.FullyQualifiedTypeName))
                {
                    unique.Add(actor);
                }
            }

            // Sort for deterministic output
            unique.Sort((a, b) => string.Compare(a.FullyQualifiedTypeName, b.FullyQualifiedTypeName, StringComparison.Ordinal));

            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ActorDiscoveryCount,
                Location.None,
                unique.Count));

            if (unique.Count > 0)
            {
                var source = GenerateRegistrationSource(unique);
                spc.AddSource("Dapr_VirtualActors_Registration.g.cs", source);
            }
        });
    }

    private static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, baseType) ||
                SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }
        return false;
    }

    private static DiscoveredActor BuildDiscoveredActor(INamedTypeSymbol symbol, INamedTypeSymbol? iVirtualActor)
    {
        var interfaces = new List<string>();

        if (iVirtualActor is not null)
        {
            foreach (var iface in symbol.AllInterfaces)
            {
                // Include interfaces that derive from IVirtualActor but aren't IVirtualActor itself
                if (!SymbolEqualityComparer.Default.Equals(iface, iVirtualActor) &&
                    iface.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iVirtualActor)))
                {
                    interfaces.Add(iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }
        }

        // Find constructors to determine if actor takes only VirtualActorHost
        var hasHostOnlyCtor = symbol.InstanceConstructors.Any(c =>
            c.Parameters.Length == 1 &&
            c.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .EndsWith("VirtualActorHost", StringComparison.Ordinal));

        return new DiscoveredActor(
            FullyQualifiedTypeName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            SimpleName: symbol.Name,
            Interfaces: interfaces.ToImmutableArray(),
            HasHostOnlyCtor: hasHostOnlyCtor);
    }

    private static string GenerateRegistrationSource(IReadOnlyList<DiscoveredActor> actors)
    {
        var sb = new StringBuilder(2048);

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Dapr.VirtualActors.Generators");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Dapr.VirtualActors;");
        sb.AppendLine("using Dapr.VirtualActors.Runtime;");
        sb.AppendLine();
        sb.AppendLine("namespace Dapr.VirtualActors.Runtime");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated registration of discovered VirtualActor types.");
        sb.AppendLine("    /// Provides AOT-safe factory delegates with no reflection.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class GeneratedActorRegistration");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all discovered actor types into the provided options.");
        sb.AppendLine("        /// Called automatically during host startup.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static void RegisterDiscoveredActors(VirtualActorOptions options)");
        sb.AppendLine("        {");

        foreach (var actor in actors)
        {
            // Generate the interface type array as a static readonly to avoid per-call allocation
            sb.AppendLine($"            options.RegisterActor(");
            sb.AppendLine($"                actorTypeName: {CodeLiteral(actor.SimpleName)},");

            // Generate interface types list
            if (actor.Interfaces.Length == 0)
            {
                sb.AppendLine($"                interfaceTypes: Array.Empty<Type>(),");
            }
            else
            {
                sb.Append("                interfaceTypes: new Type[] { ");
                sb.Append(string.Join(", ", actor.Interfaces.Select(i => $"typeof({i})")));
                sb.AppendLine(" },");
            }

            sb.AppendLine($"                implementationType: typeof({actor.FullyQualifiedTypeName}),");

            // Generate AOT-safe factory delegate
            if (actor.HasHostOnlyCtor)
            {
                sb.AppendLine($"                factory: static (host, _) => new {actor.FullyQualifiedTypeName}(host));");
            }
            else
            {
                // For actors with additional constructor parameters, we'd need DI resolution.
                // Since we can't use ActivatorUtilities (reflection), generate a factory that
                // resolves from the scoped service provider.
                sb.AppendLine($"                factory: static (host, sp) =>");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    // Actor has additional constructor dependencies resolved from DI.");
                sb.AppendLine($"                    // The source generator detected a constructor that requires more than just VirtualActorHost.");
                sb.AppendLine($"                    // Developers should register a custom factory for this actor type.");
                sb.AppendLine($"                    throw new InvalidOperationException(");
                sb.AppendLine($"                        \"Actor type '{actor.SimpleName}' has a constructor with parameters beyond VirtualActorHost. \" +");
                sb.AppendLine($"                        \"Register it explicitly with a factory delegate: \" +");
                sb.AppendLine($"                        \"options.RegisterActor<{actor.SimpleName}>(host => new {actor.SimpleName}(host, ...))\");");
                sb.AppendLine($"                }});");
            }

            sb.AppendLine();
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate a module initializer that hooks into the options configuration
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Module initializer that registers the auto-discovery hook.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class GeneratedActorRegistrationModule");
        sb.AppendLine("    {");
        sb.AppendLine("        [ModuleInitializer]");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        sb.AppendLine("            VirtualActorAutoRegistration.RegisterDiscoveryHook(GeneratedActorRegistration.RegisterDiscoveredActors);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string CodeLiteral(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

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
}

/// <summary>
/// Represents a discovered actor type from compilation analysis.
/// </summary>
internal sealed record DiscoveredActor(
    string FullyQualifiedTypeName,
    string SimpleName,
    ImmutableArray<string> Interfaces,
    bool HasHostOnlyCtor);

/// <summary>
/// Cached known symbol references used by the generator.
/// </summary>
internal sealed record KnownSymbols(
    INamedTypeSymbol? VirtualActorBase,
    INamedTypeSymbol? VirtualActorHost,
    INamedTypeSymbol? IVirtualActor);
