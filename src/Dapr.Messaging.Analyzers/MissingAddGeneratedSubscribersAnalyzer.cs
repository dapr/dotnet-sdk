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
/// Reports DAPR1614 when an application calls <c>AddDaprSubscriber()</c> and has at least
/// one <c>[DaprTopic]</c>-annotated handler, but never calls the source-generated
/// <c>AddGeneratedSubscribers()</c> extension, meaning the handler(s) will never be
/// registered with the dispatcher/registry and won't receive events.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingAddGeneratedSubscribersAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DAPR1614";

    private const string DaprTopicAttributeFqn = "Dapr.Messaging.DaprTopicAttribute";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Call AddGeneratedSubscribers to register Dapr topic handlers",
        messageFormat: "Call 'AddGeneratedSubscribers()' after 'AddDaprSubscriber()' so source-generated Dapr topic handlers are registered with the dispatcher",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AddDaprSubscriber() only registers gRPC push plumbing. The generated AddGeneratedSubscribers() extension must also be called to register [DaprTopic] handlers with the dispatcher/registry, otherwise no topic subscriptions are ever added.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var topicAttr = compilationStartContext.Compilation.GetTypeByMetadataName(DaprTopicAttributeFqn);

            bool hasAddGeneratedSubscribers = false;
            InvocationExpressionSyntax? addDaprSubscriberInvocation = null;
            bool hasDaprTopicHandler = false;
            var syncLock = new object();

            // 1. Syntax action to locate AddDaprSubscriber and AddGeneratedSubscribers invocations
            compilationStartContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                if (syntaxContext.Node is not InvocationExpressionSyntax invocation)
                {
                    return;
                }

                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Name.Identifier.Text == "AddGeneratedSubscribers")
                    {
                        lock (syncLock)
                        {
                            hasAddGeneratedSubscribers = true;
                        }
                    }
                    else if (memberAccess.Name.Identifier.Text == "AddDaprSubscriber")
                    {
                        lock (syncLock)
                        {
                            addDaprSubscriberInvocation ??= invocation;
                        }
                    }
                }
            }, SyntaxKind.InvocationExpression);

            // 2. Symbol action to locate any [DaprTopic] handlers
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
                            lock (syncLock)
                            {
                                hasDaprTopicHandler = true;
                            }
                            break;
                        }
                    }
                }, SymbolKind.NamedType);
            }

            // 3. Compilation end action to report diagnostic if AddGeneratedSubscribers is missing
            compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
            {
                lock (syncLock)
                {
                    if (!hasAddGeneratedSubscribers && hasDaprTopicHandler && addDaprSubscriberInvocation is not null)
                    {
                        compilationEndContext.ReportDiagnostic(
                            Diagnostic.Create(Rule, addDaprSubscriberInvocation.GetLocation()));
                    }
                }
            });
        });
    }
}
