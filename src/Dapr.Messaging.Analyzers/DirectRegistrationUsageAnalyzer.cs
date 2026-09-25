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
/// Reports DAPR1614 when application code calls <c>DaprMessagingRegistration.Register</c> directly rather
/// than going through the source-generated <c>AddDaprMessaging</c> extension method.
/// </summary>
/// <remarks>
/// <c>DaprMessagingRegistration</c> is public only so that generated code compiled into a consumer's
/// assembly can call it. Calling it by hand bypasses the generator's feature detection, which is what
/// decides whether gRPC server hosting and HTTP routing are registered, and so can produce a messaging
/// stack that silently fails to receive messages.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectRegistrationUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DAPR1614";

    private const string RegistrationTypeFqn = "Dapr.Messaging.DaprMessagingRegistration";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Do not call DaprMessagingRegistration directly",
        messageFormat: "Call the generated 'services.AddDaprMessaging()' extension instead of 'DaprMessagingRegistration.{0}'",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DaprMessagingRegistration is an implementation detail intended for source-generated code. Calling it directly bypasses the generator's delivery-mode detection, which determines whether gRPC server hosting and HTTP routing are registered.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // Generated code is the legitimate caller, so it must be excluded from analysis.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var registrationType = compilationStartContext.Compilation.GetTypeByMetadataName(RegistrationTypeFqn);
            if (registrationType is null)
            {
                return;
            }

            compilationStartContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var invocation = (InvocationExpressionSyntax)syntaxContext.Node;

                if (syntaxContext.SemanticModel.GetSymbolInfo(invocation, syntaxContext.CancellationToken).Symbol
                    is not IMethodSymbol method)
                {
                    return;
                }

                if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, registrationType))
                {
                    return;
                }

                syntaxContext.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
            }, SyntaxKind.InvocationExpression);
        });
    }
}
