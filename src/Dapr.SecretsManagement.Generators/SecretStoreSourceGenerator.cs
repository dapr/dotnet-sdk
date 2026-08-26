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

namespace Dapr.SecretsManagement;

/// <summary>
/// Incremental source generator that discovers interfaces annotated with <c>[SecretStore]</c>,
/// reads their property mappings (including optional <c>[Secret]</c> attributes), and emits:
/// <list type="bullet">
///   <item>A concrete implementation class that stores secret values retrieved from the Dapr secret store.</item>
///   <item>A DI registration extension method on <c>IDaprSecretsManagementBuilder</c>.</item>
/// </list>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SecretStoreSourceGenerator : IIncrementalGenerator
{
    private const string SecretStoreAttributeFullName = "Dapr.SecretsManagement.Abstractions.SecretStoreAttribute";
    private const string SecretAttributeFullName = "Dapr.SecretsManagement.Abstractions.SecretAttribute";
    private const string ScanReferencesPropertyName = "build_property.DaprSecretsManagementScanReferences";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Cache the attribute symbols.
        var known = context.CompilationProvider.Select((c, _) =>
            new KnownSymbols(
                SecretStoreAttribute: c.GetTypeByMetadataName(SecretStoreAttributeFullName),
                SecretAttribute: c.GetTypeByMetadataName(SecretAttributeFullName)));

        // Read the ScanReferences MSBuild property.
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue(ScanReferencesPropertyName, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        // Report diagnostic if the attribute types are not found.
        context.RegisterSourceOutput(known, (spc, ks) =>
        {
            if (ks.SecretStoreAttribute is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPR1601",
                        "SecretStore attribute not found",
                        "The source generator could not find the SecretStoreAttribute type. " +
                        "Ensure that the Dapr.SecretsManagement package is properly referenced.",
                        "Dapr.SecretsManagement",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None));
            }
        });

        // Discover candidate interface declarations with at least one attribute.
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is InterfaceDeclarationSyntax ids && ids.AttributeLists.Count > 0,
            transform: static (ctx, ct) =>
            {
                var interfaceSyntax = (InterfaceDeclarationSyntax)ctx.Node;
                var symbol = ctx.SemanticModel.GetDeclaredSymbol(interfaceSyntax, ct);
                return symbol as INamedTypeSymbol;
            })
            .Where(static s => s is not null)!;

        // Referenced-assembly pipeline: when ScanReferences is enabled, enumerate interfaces
        // from referenced assemblies so types authored in F# (or other non-C# languages) are
        // discovered by the generator.
        var referencedInterfaces = context.CompilationProvider
            .Combine(known)
            .Combine(scanReferences)
            .SelectMany((input, _) =>
            {
                var ((compilation, ks), scan) = input;
                if (!scan || ks.SecretStoreAttribute is null)
                    return ImmutableArray<INamedTypeSymbol>.Empty;

                var list = new List<INamedTypeSymbol>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    foreach (var type in EnumerateTypes(assembly.GlobalNamespace))
                    {
                        if (type.TypeKind == TypeKind.Interface)
                            list.Add(type);
                    }
                }
                return list.ToImmutableArray();
            });

        // Merge syntax-discovered and reference-discovered candidates, deduplicating by symbol identity.
        var allCandidates = candidates.Collect()
            .Combine(referencedInterfaces.Collect())
            .Select((input, _) =>
            {
                var (fromSyntax, fromReferences) = input;
                var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var result = new List<INamedTypeSymbol>();
                foreach (var s in fromSyntax)
                {
                    if (s is not null && seen.Add(s))
                        result.Add(s);
                }
                foreach (var r in fromReferences)
                {
                    if (seen.Add(r))
                        result.Add(r);
                }
                return result.ToImmutableArray();
            });

        // Combine merged candidates with known symbols and generate for each unique interface.
        var combined = allCandidates.Combine(known);

        context.RegisterSourceOutput(combined, (spc, pair) =>
        {
            var (interfaces, ks) = pair;
            if (ks.SecretStoreAttribute is null)
                return;

            foreach (var interfaceSymbol in interfaces)
            {
                ProcessInterface(spc, interfaceSymbol, ks);
            }
        });
    }

    private static void ProcessInterface(
        SourceProductionContext spc,
        INamedTypeSymbol interfaceSymbol,
        KnownSymbols ks)
    {
        // Check if the interface has [SecretStore] attribute.
        var storeAttrData = interfaceSymbol.GetAttributes().FirstOrDefault(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, ks.SecretStoreAttribute));

        if (storeAttrData is null)
            return;

        // Extract the store name from the constructor argument.
        if (storeAttrData.ConstructorArguments.Length < 1 ||
            storeAttrData.ConstructorArguments[0].Value is not string storeName)
            return;

        // Collect properties and their secret key mappings.
        var properties = new List<(string PropertyName, string SecretKey, string PropertyType)>();

        foreach (var member in interfaceSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop)
                continue;

            if (prop.GetMethod is null)
                continue;

            var secretKey = prop.Name;

            // Check for [Secret("key")] override.
            if (ks.SecretAttribute is not null)
            {
                var secretAttrData = prop.GetAttributes().FirstOrDefault(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, ks.SecretAttribute));

                if (secretAttrData is not null &&
                    secretAttrData.ConstructorArguments.Length > 0 &&
                    secretAttrData.ConstructorArguments[0].Value is string overrideName)
                {
                    secretKey = overrideName;
                }
            }

            properties.Add((prop.Name, secretKey, prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        if (properties.Count == 0)
            return;

        // Resolve naming.
        var interfaceName = interfaceSymbol.Name;
        var namespaceName = interfaceSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : interfaceSymbol.ContainingNamespace.ToDisplayString();

        // Implementation class name: strip leading 'I' from interface name, append "SecretStoreClient".
        var implName = interfaceName.StartsWith("I", StringComparison.Ordinal) && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1) + "SecretStoreClient"
            : interfaceName + "SecretStoreClient";

        var source = GenerateSource(
            namespaceName, interfaceName, implName, storeName,
            interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            properties);

        var fqn = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var hintName = implName + "_" + GetStableHash(fqn).ToString("X8") + ".g.cs";
        spc.AddSource(hintName, source);
    }

    private static string GenerateSource(
        string? namespaceName,
        string interfaceName,
        string implName,
        string storeName,
        string fullyQualifiedInterfaceName,
        List<(string PropertyName, string SecretKey, string PropertyType)> properties)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.Hosting;");
        sb.AppendLine();

        if (namespaceName is not null)
        {
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
        }

        // 1. Implementation class
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Auto-generated implementation of <see cref=\"{interfaceName}\"/> that retrieves secrets from the");
        sb.AppendLine($"    /// Dapr secret store component named <c>{EscapeXml(storeName)}</c>.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [GeneratedCode(\"Dapr.SecretsManagement.Generators\", \"1.0.0\")]");
        sb.AppendLine($"    internal sealed class {implName} : {fullyQualifiedInterfaceName}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly Dictionary<string, string> _secrets;");
        sb.AppendLine();
        sb.AppendLine($"        internal {implName}(Dictionary<string, string> secrets)");
        sb.AppendLine("        {");
        sb.AppendLine("            _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var (propName, secretKey, propType) in properties)
        {
            sb.AppendLine($"        /// <inheritdoc />");
            sb.AppendLine($"        public {propType} {propName} =>");
            sb.AppendLine($"            _secrets.TryGetValue(\"{EscapeCSharpString(secretKey)}\", out var __{propName}Value)");
            sb.AppendLine($"                ? __{propName}Value");
            sb.AppendLine($"                : throw new KeyNotFoundException(\"Secret '{EscapeCSharpString(secretKey)}' was not found in store '{EscapeCSharpString(storeName)}'.\");");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // 2. Hosted service that loads secrets at startup
        var loaderName = implName.Replace("SecretStoreClient", "SecretStoreLoader");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Hosted service that pre-loads secrets from the <c>{EscapeXml(storeName)}</c> Dapr secret store");
        sb.AppendLine($"    /// at application startup and registers the generated <see cref=\"{implName}\"/> in the container.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [GeneratedCode(\"Dapr.SecretsManagement.Generators\", \"1.0.0\")]");
        sb.AppendLine($"    internal sealed class {loaderName} : IHostedService");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly Dapr.SecretsManagement.DaprSecretsManagementClient _client;");
        sb.AppendLine($"        private Dictionary<string, string>? _secrets;");
        sb.AppendLine();
        sb.AppendLine($"        public {loaderName}(Dapr.SecretsManagement.DaprSecretsManagementClient client)");
        sb.AppendLine("        {");
        sb.AppendLine("            _client = client ?? throw new ArgumentNullException(nameof(client));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        internal Dictionary<string, string> Secrets =>");
        sb.AppendLine("            _secrets ?? throw new InvalidOperationException(");
        sb.AppendLine($"                \"Secrets from store '{EscapeCSharpString(storeName)}' have not been loaded yet. Ensure the host has started.\");");
        sb.AppendLine();
        sb.AppendLine("        public async Task StartAsync(CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var bulk = await _client.GetBulkSecretAsync(\"{EscapeCSharpString(storeName)}\", cancellationToken: cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("            var flat = new Dictionary<string, string>();");
        sb.AppendLine("            foreach (var entry in bulk)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var kvp in entry.Value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    flat[kvp.Key] = kvp.Value;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            _secrets = flat;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // 3. DI registration extension
        var methodName = "Add" + (interfaceName.StartsWith("I", StringComparison.Ordinal) && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1)
            : interfaceName);

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Extension methods for registering the generated <see cref=\"{implName}\"/> implementation.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [GeneratedCode(\"Dapr.SecretsManagement.Generators\", \"1.0.0\")]");
        sb.AppendLine($"    public static class {implName}Extensions");
        sb.AppendLine("    {");
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Registers the generated typed secret store implementation for <see cref=\"{interfaceName}\"/>");
        sb.AppendLine($"        /// with the dependency injection container. Secrets are loaded from the <c>{EscapeXml(storeName)}</c>");
        sb.AppendLine($"        /// Dapr secret store at application startup via an <see cref=\"IHostedService\"/>.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"builder\">The Dapr Secrets Management builder.</param>");
        sb.AppendLine($"        /// <returns>The builder instance for chaining.</returns>");
        sb.AppendLine($"        public static Dapr.SecretsManagement.IDaprSecretsManagementBuilder {methodName}(");
        sb.AppendLine($"            this Dapr.SecretsManagement.IDaprSecretsManagementBuilder builder)");
        sb.AppendLine("        {");
        sb.AppendLine($"            builder.Services.AddSingleton<{loaderName}>();");
        sb.AppendLine($"            builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<{loaderName}>());");
        sb.AppendLine($"            builder.Services.AddSingleton<{fullyQualifiedInterfaceName}>(sp =>");
        sb.AppendLine($"                new {implName}(sp.GetRequiredService<{loaderName}>().Secrets));");
        sb.AppendLine("            return builder;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        if (namespaceName is not null)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string EscapeCSharpString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Produces a stable, deterministic 32-bit hash from the given string.
    /// Used to generate unique hint names when multiple types share the same simple name
    /// across different namespaces.
    /// </summary>
    private static uint GetStableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in text)
            {
                hash = (hash ^ ch) * 16777619;
            }
            return hash;
        }
    }

    /// <summary>
    /// Recursively enumerates all named types in a namespace and its child namespaces, including nested types.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in EnumerateNestedTypes(type))
            {
                yield return nested;
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var type in EnumerateTypes(child))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in EnumerateNestedTypes(nested))
            {
                yield return child;
            }
        }
    }
}
