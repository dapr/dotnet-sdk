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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Messaging.Analyzers;

/// <summary>
/// Reports DAPR1612 (warning) when an <c>ITopicHandler&lt;T&gt;</c> implementation's message type
/// <c>T</c> is not covered by any <c>[JsonSerializable(typeof(T))]</c> attribute in the compilation
/// and is not a primitive/string/byte[] type. Native AOT publishing may fail unless the type is added
/// to a source-generated <c>JsonSerializerContext</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonAotCompatibleMessageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DAPR1612";

    private const string ITopicHandlerFqn = "Dapr.Messaging.ITopicHandler`1";
    private const string JsonSerializableAttributeFqn = "System.Text.Json.Serialization.JsonSerializableAttribute";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Message type not registered in a JsonSerializerContext",
        messageFormat: "Message type '{0}' is not registered in a JsonSerializerContext; native AOT publishing may fail unless this type is added",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ITopicHandler<T> message types should be registered in a source-generated JsonSerializerContext for native AOT compatibility.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var handlerInterface = compilationStartContext.Compilation.GetTypeByMetadataName(ITopicHandlerFqn);
            if (handlerInterface is null)
            {
                return;
            }

            var jsonSerializable = compilationStartContext.Compilation.GetTypeByMetadataName(JsonSerializableAttributeFqn);
            var registeredTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            // Collect every type T that appears as [JsonSerializable(typeof(T))] anywhere in the
            // compilation's assembly + referenced assemblies.
            if (jsonSerializable is not null)
            {
                CollectJsonSerializableTypes(compilationStartContext.Compilation.Assembly.GlobalNamespace, jsonSerializable, registeredTypes);
                foreach (var reference in compilationStartContext.Compilation.References)
                {
                    if (compilationStartContext.Compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asm)
                    {
                        CollectJsonSerializableTypes(asm.GlobalNamespace, jsonSerializable, registeredTypes);
                    }
                }
            }

            compilationStartContext.RegisterSymbolAction(symbolContext =>
            {
                if (symbolContext.Symbol is not INamedTypeSymbol type)
                {
                    return;
                }

                foreach (var iface in type.AllInterfaces)
                {
                    if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, handlerInterface) || iface.TypeArguments.Length != 1)
                    {
                        continue;
                    }

                    if (iface.TypeArguments[0] is not INamedTypeSymbol message)
                    {
                        continue;
                    }

                    if (IsPrimitiveLike(message) || registeredTypes.Contains(message))
                    {
                        continue;
                    }

                    symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, iface.Locations.FirstOrDefault(), message.ToDisplayString()));
                }
            }, SymbolKind.NamedType);
        });
    }

    private static void CollectJsonSerializableTypes(INamespaceSymbol ns, INamedTypeSymbol jsonSerializableAttr, HashSet<ITypeSymbol> bucket)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            foreach (var attr in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, jsonSerializableAttr))
                {
                    continue;
                }

                if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is INamedTypeSymbol registered)
                {
                    bucket.Add(registered);
                }
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectJsonSerializableTypes(child, jsonSerializableAttr, bucket);
        }
    }

    private static bool IsPrimitiveLike(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None)
        {
            return true;
        }

        var name = type.ToDisplayString();
        return name == "byte[]";
    }
}
