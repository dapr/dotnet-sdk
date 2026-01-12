// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// An analyzer for Dapr Actors that validates that whenever a register is registered, the method specified to invoke
/// as the callback should actually exist on the type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TimerCallbackMethodPresentAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor DaprTimerCallbackMethodRule = new(
        id: "DAPR1401",
        title: new LocalizableResourceString(nameof(Resources.DAPR1401Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1401MessageFormat), Resources.ResourceManager, typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DaprTimerCallbackMethodRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);        
    }
    
    private void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;
        
        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocationExpr.Expression).Symbol as IMethodSymbol;
        
        if (methodSymbol is null 
            || methodSymbol.Name != "RegisterTimerAsync"
            || methodSymbol.ContainingType.Name != "Actor"
            || methodSymbol.ContainingType.ContainingNamespace.ToDisplayString() != "Dapr.Actors.Runtime")
        {
            return;
        }

        var ancestorType = invocationExpr.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (ancestorType is null)
        {
            return;
        }
        var ancestorTypeSymbol = context.SemanticModel.GetDeclaredSymbol(ancestorType);
        if (ancestorTypeSymbol is null)
        {
            return;
        }
        var ancestorTypeName = ancestorTypeSymbol?.Name;
        
        var arguments = invocationExpr.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return;
        }

        var secondArgument = arguments[1].Expression;
        var methodNameValue = context.SemanticModel.GetConstantValue(secondArgument);
        if (!methodNameValue.HasValue || methodNameValue.Value is not string methodName)
        {
            return;
        }

        var members = ancestorTypeSymbol?.GetMembers().OfType<IMethodSymbol>().ToList();
        var methodExists = members?.Any(m => m.Name == methodName) == true;
        if (!methodExists)
        {
            //Get the type that contains our RegisterTimerAsync method
            
            var diagnostic = Diagnostic.Create(SupportedDiagnostics[0], secondArgument.GetLocation(), methodName,
                ancestorTypeName);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
