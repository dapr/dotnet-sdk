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
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.Vector.Generators;

/// <summary>Generates typed, collection-bound Vector clients and their DI registration extensions.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class VectorCollectionSourceGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Dapr.Vector.VectorCollectionAttribute";
    private const string CollectionInterfaceName = "Dapr.Vector.IDaprVectorCollection`1";

    private static readonly DiagnosticDescriptor InvalidDeclaration = new(
        "DAPR1901", "Unsupported vector collection declaration",
        "'{0}' must be a top-level, non-generic, non-file-local partial interface",
        "Dapr.Vector", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidBinding = new(
        "DAPR1902", "Invalid vector collection binding",
        "'{0}' must specify non-empty component and collection names",
        "Dapr.Vector", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidContract = new(
        "DAPR1903", "Invalid vector collection contract",
        "'{0}' must extend exactly one IDaprVectorCollection<T> and declare no additional abstract members",
        "Dapr.Vector", DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var indexes = context.SyntaxProvider.ForAttributeWithMetadataName(AttributeName,
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, _) => ctx);
        context.RegisterSourceOutput(indexes, static (output, ctx) => Generate(output, ctx));
    }

    private static void Generate(SourceProductionContext output, GeneratorAttributeSyntaxContext context)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var declaration = (InterfaceDeclarationSyntax)context.TargetNode;
        if (symbol.ContainingType is not null || symbol.Arity != 0 || symbol.IsFileLocal ||
            !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            Report(InvalidDeclaration);
            return;
        }

        var arguments = context.Attributes[0].ConstructorArguments;
        if (arguments.Length != 2 || arguments[0].Value is not string store ||
            arguments[1].Value is not string collection || string.IsNullOrWhiteSpace(store) ||
            string.IsNullOrWhiteSpace(collection))
        {
            Report(InvalidBinding);
            return;
        }

        var contract = context.SemanticModel.Compilation.GetTypeByMetadataName(CollectionInterfaceName);
        var bases = symbol.AllInterfaces
            .Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, contract)).ToArray();
        if (bases.Length != 1 || symbol.AllInterfaces.Concat(new[] { symbol })
                .Where(i => !SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, contract))
                .Any(i => i.GetMembers().Any(member => member.IsAbstract)))
        {
            Report(InvalidContract);
            return;
        }

        var typeFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        var type = bases[0].TypeArguments[0].ToDisplayString(typeFormat);
        var name = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var ns = symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();
        var implementation = $"__{symbol.Name}VectorCollectionClient";
        var extensions = $"{symbol.Name}VectorCollectionExtensions";
        var baseName = symbol.Name.StartsWith("I", StringComparison.Ordinal) && symbol.Name.Length > 1
            ? symbol.Name.Substring(1)
            : symbol.Name;
        var visibility = symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        if (ns is not null)
            source.AppendLine($"namespace {ns} {{");
        source.AppendLine($$"""
                            internal sealed class {{implementation}} : {{name}}
                            {
                                private const string StoreName = {{SymbolDisplay.FormatLiteral(store, true)}};
                                private const string CollectionName = {{SymbolDisplay.FormatLiteral(collection, true)}};
                                private readonly global::Dapr.Vector.DaprVectorClient client;

                                internal {{implementation}}(global::Dapr.Vector.DaprVectorClient client)
                                {
                                    this.client = client ?? throw new global::System.ArgumentNullException(nameof(client));
                                }
                            """);

        const string task = "global::System.Threading.Tasks.Task";
        const string metadata =
            "global::System.Collections.Generic.IReadOnlyDictionary<string, string>? metadata = null";
        const string token = "global::System.Threading.CancellationToken cancellationToken = default";
        const string options = "global::Dapr.Vector.IndexingOptions? options = null";
        const string ids = "global::System.Collections.Generic.IReadOnlyList<string> ids";
        var recordList = $"global::System.Collections.Generic.IReadOnlyList<global::Dapr.Vector.VectorRecord<{type}>>";
        AppendMethod(task, "CreateCollectionAsync",
            $"uint dimensions, global::Dapr.Vector.DistanceMetric metric = global::Dapr.Vector.DistanceMetric.Unspecified, {metadata}, {token}",
            "dimensions, metric, metadata, cancellationToken");
        AppendMethod($"{task}<global::Dapr.Vector.VectorCollection>", "GetCollectionAsync", $"{metadata}, {token}",
            "metadata, cancellationToken");
        AppendMethod(task, "DeleteCollectionAsync", $"{metadata}, {token}", "metadata, cancellationToken");
        AppendMethod($"{task}<global::Dapr.Vector.UpsertVectorsResult>", "UpsertVectorsAsync",
            $"{recordList} records, {options}, {metadata}, {token}", "records, options, metadata, cancellationToken",
            type);
        AppendMethod($"{task}<{recordList}>", "GetVectorsAsync",
            $"{ids}, bool? includeValues = null, {metadata}, {token}",
            "ids, includeValues, metadata, cancellationToken", type);
        AppendMethod($"{task}<global::Dapr.Vector.IndexAck>", "DeleteVectorsAsync",
            $"{ids}, {options}, {metadata}, {token}", "ids, options, metadata, cancellationToken");
        AppendMethod($"{task}<global::Dapr.Vector.VectorQueryResult<{type}>>", "QueryVectorsAsync",
            $"global::Dapr.Vector.VectorQuery query, {metadata}, {token}", "query, metadata, cancellationToken", type);
        AppendMethod(
            $"{task}<global::System.Collections.Generic.IReadOnlyList<global::Dapr.Vector.VectorBatchQueryResult<{type}>>>",
            "BatchQueryVectorsAsync",
            $"global::System.Collections.Generic.IReadOnlyList<global::Dapr.Vector.VectorQuery> queries, {metadata}, {token}",
            "queries, metadata, cancellationToken", type);
        source.AppendLine("}");
        source.AppendLine($$"""
                            /// <summary>Registers the generated vector collection using the underlying client's service lifetime.</summary>
                            {{visibility}} static class {{extensions}}
                            {
                                /// <summary>Registers the typed collection without taking ownership of its underlying client.</summary>
                                public static global::Dapr.Vector.Extensions.IDaprVectorBuilder Add{{baseName}}(
                                    this global::Dapr.Vector.Extensions.IDaprVectorBuilder builder)
                                {
                                    if (builder is null)
                                        throw new global::System.ArgumentNullException(nameof(builder));
                                    var registration = global::System.Linq.Enumerable.LastOrDefault(builder.Services,
                                        descriptor => descriptor.ServiceType == typeof(global::Dapr.Vector.DaprVectorClient) && !descriptor.IsKeyedService);
                                    if (registration is null)
                                        throw new global::System.InvalidOperationException("Register DaprVectorClient with AddDaprVectorClient before registering a typed vector collection.");
                                    builder.Services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(
                                        typeof({{name}}),
                                        provider => new {{implementation}}(
                                            global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Dapr.Vector.DaprVectorClient>(provider)),
                                        registration.Lifetime));
                                    return builder;
                                }
                            }
                            """);
        if (ns is not null)
            source.AppendLine("}");
        output.AddSource(name.Replace("global::", "").Replace("@", "") + ".VectorCollection.g.cs",
            SourceText.From(source.ToString(), Encoding.UTF8));

        void Report(DiagnosticDescriptor descriptor) =>
            output.ReportDiagnostic(Diagnostic.Create(descriptor, declaration.Identifier.GetLocation(), symbol.Name));

        void AppendMethod(string returnType, string method, string parameters, string forwarded,
            string? genericType = null)
        {
            var generic = genericType is null ? "" : $"<{genericType}>";
            source.AppendLine($"    /// <inheritdoc />");
            source.AppendLine($"    public {returnType} {method}({parameters}) =>");
            source.AppendLine($"        client.{method}{generic}(StoreName, CollectionName, {forwarded});");
        }
    }
}
