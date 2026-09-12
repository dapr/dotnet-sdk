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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Messaging.Analyzers;

/// <summary>
/// Reports DAPR1611 when <c>[DaprTopic]</c> is applied to a class that does not implement
/// <c>ITopicHandler&lt;T&gt;</c> or <c>ITopicHandler&lt;T, TResult&gt;</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerNotImplementingITopicHandlerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DAPR1611";

    private const string DaprTopicAttributeFqn = "Dapr.Messaging.DaprTopicAttribute";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Handler must implement ITopicHandler<T>",
        messageFormat: "Type '{0}' is annotated with [DaprTopic] but does not implement ITopicHandler<T> or ITopicHandler<T, TResult>",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type annotated with [DaprTopic] must implement ITopicHandler<T> or ITopicHandler<T, TResult>.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var topicAttr = compilationStartContext.Compilation.GetTypeByMetadataName(DaprTopicAttributeFqn);
            if (topicAttr is null)
            {
                return;
            }

            compilationStartContext.RegisterSymbolAction(symbolContext =>
            {
                if (symbolContext.Symbol is not INamedTypeSymbol type)
                {
                    return;
                }

                var hasTopic = type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, topicAttr));
                if (!hasTopic)
                {
                    return;
                }

                var implementsHandler = type.AllInterfaces.Any(i => i.OriginalDefinition is { IsGenericType: true } def
                    && def.ContainingNamespace?.ToDisplayString() == "Dapr.Messaging"
                    && def.Name == "ITopicHandler");

                if (!implementsHandler)
                {
                    symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations.FirstOrDefault(), type.ToDisplayString()));
                }
            }, SymbolKind.NamedType);
        });
    }
}
