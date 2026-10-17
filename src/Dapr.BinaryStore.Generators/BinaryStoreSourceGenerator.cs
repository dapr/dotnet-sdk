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

using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.BinaryStore;

/// <summary>
/// Incremental source generator that emits a typed Dapr binary-store client wrapper for every
/// <c>partial interface</c> annotated with <c>[BinaryStore("storeName")]</c> that extends
/// <c>IDaprBinaryStoreClient</c>.
/// </summary>
/// <remarks>
/// For each matching interface the generator produces a single <c>.g.cs</c> file containing:
/// <list type="number">
///   <item>A <c>sealed internal</c> implementation class that forwards all
///   <c>IDaprBinaryStoreClient</c> operations to a <c>DaprBinaryStoreClient</c> with the
///   store name pre-filled.</item>
///   <item>A <c>public static</c> extension-method class with a <c>With{Name}()</c> method on
///   <c>IDaprBinaryStoreBuilder</c> that registers the implementation with the DI container.</item>
/// </list>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class BinaryStoreSourceGenerator : IIncrementalGenerator
{
    private const string BinaryStoreAttributeFqn = "Dapr.BinaryStore.BinaryStoreAttribute";
    private const string IDaprBinaryStoreClientFqn = "Dapr.BinaryStore.IDaprBinaryStoreClient";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var knownSymbols = context.CompilationProvider.Select(static (c, _) =>
            new KnownSymbols(
                BinaryStoreAttribute: c.GetTypeByMetadataName(BinaryStoreAttributeFqn),
                IDaprBinaryStoreClient: c.GetTypeByMetadataName(IDaprBinaryStoreClientFqn)));

        context.RegisterSourceOutput(knownSymbols, static (spc, known) =>
        {
            if (known.BinaryStoreAttribute is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "DAPR1703",
                        title: "Dapr.BinaryStore reference missing",
                        messageFormat: "The [BinaryStore] attribute could not be found. Ensure Dapr.BinaryStore is referenced.",
                        category: "Dapr.BinaryStore.Generators",
                        defaultSeverity: DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    location: null));
            }
        });

        var candidateInterfaces = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
                node is InterfaceDeclarationSyntax ids && ids.AttributeLists.Count > 0,
            transform: static (ctx, _) =>
                ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol)
            .Where(static s => s is not null)!;

        var combined = candidateInterfaces.Combine(knownSymbols);
        context.RegisterSourceOutput(combined, static (spc, pair) =>
            Execute(spc, pair.Left!, pair.Right));
    }

    private static void Execute(
        SourceProductionContext context,
        INamedTypeSymbol interfaceSymbol,
        KnownSymbols known)
    {
        if (known.BinaryStoreAttribute is null || known.IDaprBinaryStoreClient is null)
            return;

        string? storeName = null;
        foreach (var attr in interfaceSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, known.BinaryStoreAttribute))
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

        bool implementsBase = false;
        foreach (var iface in interfaceSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, known.IDaprBinaryStoreClient))
            {
                implementsBase = true;
                break;
            }
        }

        if (!implementsBase)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: "DAPR1704",
                    title: "Interface must extend IDaprBinaryStoreClient",
                    messageFormat: "'{0}' is annotated with [BinaryStore] but does not extend IDaprBinaryStoreClient.",
                    category: "Dapr.BinaryStore.Generators",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                location: interfaceSymbol.Locations.Length > 0 ? interfaceSymbol.Locations[0] : null,
                messageArgs: interfaceSymbol.Name));
            return;
        }

        var namespaceName = interfaceSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : interfaceSymbol.ContainingNamespace.ToDisplayString();

        var interfaceName = interfaceSymbol.Name;
        var fqInterfaceName = string.IsNullOrEmpty(namespaceName)
            ? interfaceName
            : $"{namespaceName}.{interfaceName}";

        var baseName = interfaceName.StartsWith("I", StringComparison.Ordinal) && interfaceName.Length > 1
            ? interfaceName.Substring(1)
            : interfaceName;

        var implClassName = $"{baseName}BinaryStoreClient";
        var extensionsClassName = $"{baseName}BinaryStoreClientExtensions";
        var addMethodName = $"With{baseName}";

        // The hash keeps hint names unique for same-named interfaces in different namespaces.
        var hintName = $"{implClassName}_{Fnv1aHash(fqInterfaceName):X8}.g.cs";

        var source = BuildSource(
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
        sb.AppendLine("// Copyright 2026 The Dapr Authors");
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
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName};");
            sb.AppendLine();
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Auto-generated implementation of <see cref=\"{interfaceName}\"/> bound to");
        sb.AppendLine($"/// the <c>{storeName}</c> Dapr binary store.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[GeneratedCode(\"Dapr.BinaryStore.Generators\", \"1.0.0\")]");
        sb.AppendLine($"internal sealed class {implClassName} : {fqInterfaceName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private const string StoreName = \"{storeName}\";");
        sb.AppendLine();
        sb.AppendLine("    private readonly global::Dapr.BinaryStore.DaprBinaryStoreClient _client;");
        sb.AppendLine();
        sb.AppendLine($"    internal {implClassName}(global::Dapr.BinaryStore.DaprBinaryStoreClient client)");
        sb.AppendLine("    {");
        sb.AppendLine("        _client = client ?? throw new global::System.ArgumentNullException(nameof(client));");
        sb.AppendLine("    }");
        sb.AppendLine();

        AppendForwardingMethod(sb, "global::System.Threading.Tasks.Task", "SetBinaryFileAsync",
            "string fileName, global::System.IO.Stream content, bool overwrite = false, int chunkSize = global::Dapr.BinaryStore.DaprBinaryStoreClient.DefaultChunkSize, global::System.Threading.CancellationToken cancellationToken = default",
            "StoreName, fileName, content, overwrite, chunkSize, cancellationToken");

        AppendForwardingMethod(sb, "global::System.Threading.Tasks.Task", "SetBinaryFileAsync",
            "string fileName, global::System.ReadOnlyMemory<byte> content, bool overwrite = false, global::System.Threading.CancellationToken cancellationToken = default",
            "StoreName, fileName, content, overwrite, cancellationToken");

        AppendForwardingMethod(sb, "global::System.Collections.Generic.IAsyncEnumerable<global::System.ReadOnlyMemory<byte>>", "GetBinaryFileChunksAsync",
            "string fileName, global::System.Threading.CancellationToken cancellationToken = default",
            "StoreName, fileName, cancellationToken");

        AppendForwardingMethod(sb, "global::System.Threading.Tasks.Task", "GetBinaryFileAsync",
            "string fileName, global::System.IO.Stream destination, global::System.Threading.CancellationToken cancellationToken = default",
            "StoreName, fileName, destination, cancellationToken");

        AppendForwardingMethod(sb, "global::System.Threading.Tasks.Task<byte[]>", "GetBinaryFileBytesAsync",
            "string fileName, global::System.Threading.CancellationToken cancellationToken = default",
            "StoreName, fileName, cancellationToken");

        AppendForwardingMethod(sb, "global::System.Threading.Tasks.Task", "DeleteBinaryFileAsync",
            "string fileName, global::System.Threading.CancellationToken cancellationToken = default",
            "StoreName, fileName, cancellationToken");

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Provides the generated DI registration extension for <see cref=\"{interfaceName}\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[GeneratedCode(\"Dapr.BinaryStore.Generators\", \"1.0.0\")]");
        sb.AppendLine($"public static class {extensionsClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Registers <see cref=\"{interfaceName}\"/> as a singleton in the DI container,");
        sb.AppendLine($"    /// backed by the generated <c>{implClassName}</c> bound to the");
        sb.AppendLine($"    /// <c>{storeName}</c> Dapr binary store.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"builder\">The Dapr binary store DI builder.</param>");
        sb.AppendLine("    /// <returns>The same builder instance for chaining.</returns>");
        sb.AppendLine($"    public static global::Dapr.BinaryStore.Extensions.IDaprBinaryStoreBuilder {addMethodName}(");
        sb.AppendLine("        this global::Dapr.BinaryStore.Extensions.IDaprBinaryStoreBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        builder.Services.AddSingleton<{fqInterfaceName}>(sp =>");
        sb.AppendLine($"            new {implClassName}(sp.GetRequiredService<global::Dapr.BinaryStore.DaprBinaryStoreClient>()));");
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
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine($"    public {returnType} {methodName}({parameters}) =>");
        sb.AppendLine($"        _client.{methodName}({forwardedArgs});");
        sb.AppendLine();
    }

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
