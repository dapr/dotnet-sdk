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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Messaging.Analyzers;

/// <summary>
/// Reports DAPR1613 when an application registers programmatic Dapr subscribers
/// via <c>[DaprTopic(Delivery = DeliveryMode.Programmatic)]</c> but omits calling
/// <c>app.MapDaprAppCallback()</c> on the endpoint routing pipeline.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingMapDaprAppCallbackAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DAPR1613";

    private const string DaprTopicAttributeFqn = "Dapr.Messaging.DaprTopicAttribute";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Call MapDaprAppCallback to map endpoints for Dapr programmatic subscriptions",
        messageFormat: "Call 'app.MapDaprAppCallback()' to map endpoints for Dapr programmatic topic subscriptions",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Programmatic topic subscriptions require app.MapDaprAppCallback() to be mapped on the endpoint routing builder so the Dapr runtime can discover and push events.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var topicAttr = compilationStartContext.Compilation.GetTypeByMetadataName(DaprTopicAttributeFqn);

            bool hasMapDaprAppCallback = false;
            InvocationExpressionSyntax? addDaprMessagingInvocation = null;
            Location? programmaticHandlerLocation = null;
            var syncLock = new object();

            // 1. Syntax action to locate AddDaprMessaging and MapDaprAppCallback invocations
            compilationStartContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                if (syntaxContext.Node is not InvocationExpressionSyntax invocation)
                {
                    return;
                }

                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Name.Identifier.Text is "MapDaprAppCallback" or "MapDaprMessaging")
                    {
                        lock (syncLock)
                        {
                            hasMapDaprAppCallback = true;
                        }
                    }
                    else if (memberAccess.Name.Identifier.Text == "AddDaprMessaging")
                    {
                        lock (syncLock)
                        {
                            addDaprMessagingInvocation ??= invocation;
                        }
                    }
                }
            }, SyntaxKind.InvocationExpression);

            // 2. Symbol action to locate [DaprTopic(Delivery = DeliveryMode.Programmatic)] handlers
            if (topicAttr is not null)
            {
                compilationStartContext.RegisterSymbolAction(symbolContext =>
                {
                    if (symbolContext.Symbol is not INamedTypeSymbol type)
                    {
                        return;
                    }

                    foreach (var attr in type.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, topicAttr))
                        {
                            var delivery = "Streaming";
                            foreach (var kvp in attr.NamedArguments)
                            {
                                if (kvp.Key == "Delivery")
                                {
                                    delivery = kvp.Value.Value?.ToString() ?? "Streaming";
                                }
                            }

                            if (delivery == "Programmatic" || delivery == "1")
                            {
                                lock (syncLock)
                                {
                                    programmaticHandlerLocation ??= attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                        ?? type.Locations.FirstOrDefault();
                                }
                                break;
                            }
                        }
                    }
                }, SymbolKind.NamedType);
            }

            // 3. Compilation end action to report diagnostic if MapDaprAppCallback is missing
            compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
            {
                lock (syncLock)
                {
                    if (!hasMapDaprAppCallback && programmaticHandlerLocation is not null)
                    {
                        if (addDaprMessagingInvocation is not null)
                        {
                            compilationEndContext.ReportDiagnostic(
                                Diagnostic.Create(Rule, addDaprMessagingInvocation.GetLocation()));
                        }
                        else
                        {
                            compilationEndContext.ReportDiagnostic(
                                Diagnostic.Create(Rule, programmaticHandlerLocation));
                        }
                    }
                }
            });
        });
    }
}
