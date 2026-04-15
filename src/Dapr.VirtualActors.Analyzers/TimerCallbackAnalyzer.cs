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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.VirtualActors.Analyzers;

/// <summary>
/// Validates that timer callback method names referenced in <c>RegisterTimerAsync</c>
/// calls actually exist on the actor type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TimerCallbackAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AnalyzerDiagnostics.TimerCallbackNotFound);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a call to RegisterTimerAsync
        string? methodName = null;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            methodName = memberAccess.Name.Identifier.Text;
        else if (invocation.Expression is IdentifierNameSyntax identifier)
            methodName = identifier.Identifier.Text;

        if (methodName != "RegisterTimerAsync")
            return;

        // The callback method name is the second argument (index 1)
        if (invocation.ArgumentList.Arguments.Count < 2)
            return;

        var callbackArg = invocation.ArgumentList.Arguments[1].Expression;

        string? callbackName = null;
        if (callbackArg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            callbackName = literal.Token.ValueText;
        }
        else if (callbackArg is InvocationExpressionSyntax nameofInvocation &&
                 nameofInvocation.Expression is IdentifierNameSyntax nameofId &&
                 nameofId.Identifier.Text == "nameof" &&
                 nameofInvocation.ArgumentList.Arguments.Count == 1)
        {
            var nameofArg = nameofInvocation.ArgumentList.Arguments[0].Expression;
            if (nameofArg is IdentifierNameSyntax callbackId)
            {
                callbackName = callbackId.Identifier.Text;
            }
        }

        if (callbackName is null)
            return;

        // Find the containing class
        var containingClass = invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null)
            return;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(containingClass);
        if (classSymbol is null)
            return;

        // Check if the method exists on the class (including inherited members)
        var hasMethod = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.Name == callbackName);

        if (!hasMethod)
        {
            // Also check base types
            for (var baseType = classSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                if (baseType.GetMembers().OfType<IMethodSymbol>().Any(m => m.Name == callbackName))
                {
                    hasMethod = true;
                    break;
                }
            }
        }

        if (!hasMethod)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDiagnostics.TimerCallbackNotFound,
                callbackArg.GetLocation(),
                callbackName,
                classSymbol.Name));
        }
    }
}
