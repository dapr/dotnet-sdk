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
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.StateManagement;

/// <summary>
/// Incremental source generator that emits a typed Dapr state-store client wrapper for every
/// <c>partial interface</c> annotated with <c>[StateStore("storeName")]</c> that extends
/// <c>IDaprStateStoreClient</c>.
/// </summary>
/// <remarks>
/// For each matching interface the generator produces a single <c>.g.cs</c> file containing:
/// <list type="number">
///   <item>A <c>sealed internal</c> implementation class that forwards all
///   <c>IDaprStateStoreClient</c> operations to a <c>DaprStateManagementClient</c> with the
///   store name pre-filled.</item>
///   <item>A <c>public static</c> extension-method class with an <c>Add{Name}()</c> method on
///   <c>IDaprStateManagementBuilder</c> that registers the implementation with the DI
///   container.</item>
/// </list>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class StateStoreSourceGenerator : IIncrementalGenerator
{
    private const string StateStoreAttributeFqn = "Dapr.StateManagement.StateStoreAttribute";
    private const string IDaprStateStoreClientFqn = "Dapr.StateManagement.IDaprStateStoreClient";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Look up the well-known symbols once per compilation (cached by Roslyn).
        var knownSymbols = context.CompilationProvider.Select(static (c, _) =>
            new KnownSymbols(
                StateStoreAttribute: c.GetTypeByMetadataName(StateStoreAttributeFqn),
                IDaprStateStoreClient: c.GetTypeByMetadataName(IDaprStateStoreClientFqn)));

        // 2. Emit a diagnostic when the attribute assembly is not referenced.
        context.RegisterSourceOutput(knownSymbols, static (spc, known) =>
        {
            if (known.StateStoreAttribute is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "DAPR1701",
                        title: "Dapr.StateManagement reference missing",
                        messageFormat: "The [StateStore] attribute could not be found. Ensure Dapr.StateManagement is referenced.",
                        category: "Dapr.StateManagement.Generators",
                        defaultSeverity: DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    location: null));
            }
        });

        // 3. Fast syntactic filter: only interface declarations that have at least one attribute.
        var candidateInterfaces = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
                node is InterfaceDeclarationSyntax ids && ids.AttributeLists.Count > 0,
            transform: static (ctx, _) =>
                ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol)
            .Where(static s => s is not null)!;

        // 4. Combine with known symbols and emit generated source.
        var combined = candidateInterfaces.Combine(knownSymbols);
        context.RegisterSourceOutput(combined, static (spc, pair) =>
            Execute(spc, pair.Left!, pair.Right));
    }

    private static void Execute(
        SourceProductionContext context,
        INamedTypeSymbol interfaceSymbol,
        KnownSymbols known)
    {
        if (known.StateStoreAttribute is null || known.IDaprStateStoreClient is null)
            return;

        // Does this interface carry [StateStore]?
        string? storeName = null;
        foreach (var attr in interfaceSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, known.StateStoreAttribute))
                continue;

            if (attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is string s &&
                !string.IsNullOrEmpty(s))
            {
                storeName = s;
            }
            break;
        }

        if (storeName is null)
            return;

        // Does the interface extend IDaprStateStoreClient?
        bool implementsBase = false;
        foreach (var iface in interfaceSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, known.IDaprStateStoreClient))
            {
                implementsBase = true;
                break;
            }
        }

        if (!implementsBase)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: "DAPR1702",
                    title: "Interface must extend IDaprStateStoreClient",
                    messageFormat: "'{0}' is annotated with [StateStore] but does not extend IDaprStateStoreClient.",
                    category: "Dapr.StateManagement.Generators",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                location: interfaceSymbol.Locations.Length > 0 ? interfaceSymbol.Locations[0] : null,
                messageArgs: interfaceSymbol.Name));
            return;
        }

        string namespaceName = interfaceSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : interfaceSymbol.ContainingNamespace.ToDisplayString();

        string interfaceName = interfaceSymbol.Name;         // e.g. "IMyStateStore"
        string fqInterfaceName = string.IsNullOrEmpty(namespaceName)
            ? interfaceName
            : $"{namespaceName}.{interfaceName}";

        // Strip leading "I" when deriving the class/extension name.
        string baseName = interfaceName.StartsWith("I", StringComparison.Ordinal) && interfaceName.Length > 1
            ? interfaceName.Substring(1)
            : interfaceName;

        string implClassName = $"{baseName}StateStoreClient";
        string extensionsClassName = $"{baseName}StateStoreClientExtensions";
        string addMethodName = $"Add{baseName}";

        // Use a stable hash of the FQN to avoid hint-name collisions when two namespaces
        // define an interface with the same simple name.
        string hintName = $"{implClassName}_{Fnv1aHash(fqInterfaceName):X8}.g.cs";

        string source = BuildSource(
            namespaceName,
            interfaceName,
            fqInterfaceName,
            implClassName,
            extensionsClassName,
            addMethodName,
            storeName);

        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private static string BuildSource(
        string namespaceName,
        string interfaceName,
        string fqInterfaceName,
        string implClassName,
        string extensionsClassName,
        string addMethodName,
        string storeName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// ------------------------------------------------------------------------");
        sb.AppendLine("// Copyright 2025 The Dapr Authors");
        sb.AppendLine("// Licensed under the Apache License, Version 2.0 (the \"License\");");
        sb.AppendLine("// you may not use this file except in compliance with the License.");
        sb.AppendLine("// You may obtain a copy of the License at");
        sb.AppendLine("//     http://www.apache.org/licenses/LICENSE-2.0");
        sb.AppendLine("// Unless required by applicable law or agreed to in writing, software");
        sb.AppendLine("// distributed under the License is distributed on an \"AS IS\" BASIS,");
        sb.AppendLine("// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.");
        sb.AppendLine("// See the License for the specific language governing permissions and");
        sb.AppendLine("// limitations under the License.");
        sb.AppendLine("// ------------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Dapr.StateManagement;");
        sb.AppendLine("using Dapr.StateManagement.Extensions;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName};");
            sb.AppendLine();
        }

        // ── Concrete implementation ────────────────────────────────────────────

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Auto-generated implementation of <see cref=\"{interfaceName}\"/> bound to");
        sb.AppendLine($"/// the <c>{storeName}</c> Dapr state store.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"Dapr.StateManagement.Generators\", \"1.0.0\")]");
        sb.AppendLine($"internal sealed class {implClassName} : {fqInterfaceName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private const string StoreName = \"{storeName}\";");
        sb.AppendLine();
        sb.AppendLine("    private readonly global::Dapr.StateManagement.DaprStateManagementClient _client;");
        sb.AppendLine();
        sb.AppendLine($"    internal {implClassName}(global::Dapr.StateManagement.DaprStateManagementClient client)");
        sb.AppendLine("    {");
        sb.AppendLine("        _client = client ?? throw new global::System.ArgumentNullException(nameof(client));");
        sb.AppendLine("    }");
        sb.AppendLine();

        AppendForwardingMethod(sb, "Task<TValue?>", "GetStateAsync<TValue>",
            "string key, ConsistencyMode? consistencyMode = null, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, key, consistencyMode, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task<(TValue? Value, string? ETag)>", "GetStateAndETagAsync<TValue>",
            "string key, ConsistencyMode? consistencyMode = null, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, key, consistencyMode, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task<IReadOnlyList<BulkStateItem<TValue>>>", "GetBulkStateAsync<TValue>",
            "IReadOnlyList<string> keys, int? parallelism = null, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, keys, parallelism, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task", "SaveStateAsync<TValue>",
            "string key, TValue value, StateOptions? stateOptions = null, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, key, value, stateOptions, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task", "SaveBulkStateAsync<TValue>",
            "IReadOnlyList<SaveStateItem<TValue>> items, CancellationToken cancellationToken = default",
            "StoreName, items, cancellationToken");

        AppendForwardingMethod(sb, "Task<bool>", "TrySaveStateAsync<TValue>",
            "string key, TValue value, string etag, StateOptions? stateOptions = null, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, key, value, etag, stateOptions, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task", "DeleteStateAsync",
            "string key, StateOptions? stateOptions = null, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, key, stateOptions, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task<bool>", "TryDeleteStateAsync",
            "string key, string etag, StateOptions? stateOptions = null, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, key, etag, stateOptions, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task", "DeleteBulkStateAsync",
            "IReadOnlyList<BulkDeleteStateItem> items, CancellationToken cancellationToken = default",
            "StoreName, items, cancellationToken");

        AppendForwardingMethod(sb, "Task", "ExecuteStateTransactionAsync",
            "IReadOnlyList<StateTransactionRequest> operations, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, operations, metadata, cancellationToken");

        AppendForwardingMethod(sb, "Task<StateQueryResponse<TValue>>", "QueryStateAsync<TValue>",
            "string jsonQuery, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default",
            "StoreName, jsonQuery, metadata, cancellationToken");

        sb.AppendLine("}");
        sb.AppendLine();

        // ── DI registration extension ─────────────────────────────────────────

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Provides the generated DI registration extension for <see cref=\"{interfaceName}\"/>.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"Dapr.StateManagement.Generators\", \"1.0.0\")]");
        sb.AppendLine($"public static class {extensionsClassName}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Registers <see cref=\"{interfaceName}\"/> as a singleton in the DI container,");
        sb.AppendLine($"    /// backed by the generated <c>{implClassName}</c> bound to the");
        sb.AppendLine($"    /// <c>{storeName}</c> Dapr state store.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"builder\">The Dapr state management DI builder.</param>");
        sb.AppendLine($"    /// <returns>The same builder instance for chaining.</returns>");
        sb.AppendLine($"    public static global::Dapr.StateManagement.Extensions.IDaprStateManagementBuilder {addMethodName}(");
        sb.AppendLine($"        this global::Dapr.StateManagement.Extensions.IDaprStateManagementBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        builder.Services.AddSingleton<{fqInterfaceName}>(sp =>");
        sb.AppendLine($"            new {implClassName}(sp.GetRequiredService<global::Dapr.StateManagement.DaprStateManagementClient>()));");
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendForwardingMethod(
        StringBuilder sb,
        string returnType,
        string methodName,
        string parameters,
        string forwardedArgs)
    {
        sb.AppendLine($"    /// <inheritdoc />");
        sb.AppendLine($"    public {returnType} {methodName}({parameters}) =>");
        sb.AppendLine($"        _client.{methodName}({forwardedArgs});");
        sb.AppendLine();
    }

    /// <summary>
    /// FNV-1a 32-bit hash, used for stable hint-name disambiguation.
    /// </summary>
    private static uint Fnv1aHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char c in text)
            {
                hash ^= (byte)c;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
