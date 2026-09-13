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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Messaging.Analyzers;

/// <summary>
/// Reports DAPR1615 when an application registers Dapr subscriber hosting or endpoint
/// functionality but has no <c>[DaprTopic]</c> subscribers that can use it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedDaprSubscriberRegistrationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DAPR1615";

    private const string DaprTopicAttributeFqn = "Dapr.Messaging.DaprTopicAttribute";
    private const string ITopicHandlerFqn = "Dapr.Messaging.ITopicHandler`1";
    private const string ITopicHandlerResultFqn = "Dapr.Messaging.ITopicHandler`2";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Remove unused Dapr subscriber registration",
        messageFormat: "Call '{0}' only when the app has {1}; remove it when no matching Dapr messaging subscribers are registered",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Dapr subscriber registration and endpoint mapping calls should only be used when matching Dapr messaging subscribers are registered.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var topicAttr = compilationStartContext.Compilation.GetTypeByMetadataName(DaprTopicAttributeFqn);
            var topicHandler = compilationStartContext.Compilation.GetTypeByMetadataName(ITopicHandlerFqn);
            var topicHandlerWithResult = compilationStartContext.Compilation.GetTypeByMetadataName(ITopicHandlerResultFqn);

            bool hasProgrammaticSubscriber = false;
            bool hasHttpSubscriber = false;
            var mapDaprAppCallbackInvocations = new List<InvocationExpressionSyntax>();
            var mapDaprHttpSubscriptionsInvocations = new List<InvocationExpressionSyntax>();
            var mapDaprMessagingInvocations = new List<InvocationExpressionSyntax>();
            var syncLock = new object();

            compilationStartContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                if (syntaxContext.Node is not InvocationExpressionSyntax invocation)
                {
                    return;
                }

                if (IsDaprInvocation(syntaxContext, invocation, "MapDaprAppCallback", "DaprAppCallbackApplicationBuilderExtensions"))
                {
                    lock (syncLock)
                    {
                        mapDaprAppCallbackInvocations.Add(invocation);
                    }
                }
                else if (IsDaprInvocation(syntaxContext, invocation, "MapDaprHttpSubscriptions", "DaprHttpSubscriptionApplicationBuilderExtensions"))
                {
                    lock (syncLock)
                    {
                        mapDaprHttpSubscriptionsInvocations.Add(invocation);
                    }
                }
                else if (IsDaprInvocation(syntaxContext, invocation, "MapDaprMessaging", "DaprMessagingEndpointRouteBuilderExtensions"))
                {
                    lock (syncLock)
                    {
                        mapDaprMessagingInvocations.Add(invocation);
                    }
                }
            }, SyntaxKind.InvocationExpression);

            if (topicAttr is not null && (topicHandler is not null || topicHandlerWithResult is not null))
            {
                compilationStartContext.RegisterSymbolAction(symbolContext =>
                {
                    if (symbolContext.Symbol is not INamedTypeSymbol type || !IsTopicHandler(type, topicHandler, topicHandlerWithResult))
                    {
                        return;
                    }

                    foreach (var attr in type.GetAttributes())
                    {
                        if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, topicAttr))
                        {
                            continue;
                        }

                        var delivery = GetDeliveryMode(attr);
                        lock (syncLock)
                        {
                            hasProgrammaticSubscriber |= delivery == "Programmatic" || delivery == "1";
                            hasHttpSubscriber |= delivery == "Http" || delivery == "2";
                        }
                    }
                }, SymbolKind.NamedType);
            }

            compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
            {
                lock (syncLock)
                {
                    if (!hasProgrammaticSubscriber)
                    {
                        Report(
                            compilationEndContext,
                            mapDaprAppCallbackInvocations,
                            "MapDaprAppCallback()",
                            "programmatic Dapr topic subscribers");
                    }

                    if (!hasHttpSubscriber)
                    {
                        Report(
                            compilationEndContext,
                            mapDaprHttpSubscriptionsInvocations,
                            "MapDaprHttpSubscriptions()",
                            "HTTP Dapr topic subscribers");
                    }

                    if (!hasProgrammaticSubscriber && !hasHttpSubscriber)
                    {
                        Report(
                            compilationEndContext,
                            mapDaprMessagingInvocations,
                            "MapDaprMessaging()",
                            "HTTP or programmatic Dapr topic subscribers");
                    }
                }
            });
        });
    }

    private static bool IsDaprInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string methodName,
        string containingTypeName)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name.Identifier.Text != methodName)
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol is null)
        {
            return true;
        }

        var definition = symbol?.ReducedFrom ?? symbol;
        return definition?.Name == methodName &&
               definition.ContainingType?.Name == containingTypeName;
    }

    private static bool IsTopicHandler(
        INamedTypeSymbol type,
        INamedTypeSymbol? topicHandler,
        INamedTypeSymbol? topicHandlerWithResult)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (topicHandler is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, topicHandler))
            {
                return true;
            }

            if (topicHandlerWithResult is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, topicHandlerWithResult))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDeliveryMode(AttributeData attr)
    {
        foreach (var kvp in attr.NamedArguments)
        {
            if (kvp.Key == "Delivery")
            {
                return kvp.Value.Value?.ToString() ?? "Streaming";
            }
        }

        return "Streaming";
    }

    private static void Report(
        CompilationAnalysisContext context,
        IEnumerable<InvocationExpressionSyntax> invocations,
        string methodName,
        string requiredSubscriberDescription)
    {
        foreach (var invocation in invocations)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                methodName,
                requiredSubscriberDescription));
        }
    }
}
