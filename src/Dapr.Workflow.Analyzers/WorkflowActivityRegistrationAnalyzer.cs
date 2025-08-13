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

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// An analyzer for Dapr workflows that validates that each workflow activity is registered with the
/// dependency injection provider.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowActivityRegistrationAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor WorkflowActivityRegistrationDescriptor = new(
        id: "DAPR1302",
        title: new LocalizableResourceString(nameof(Resources.DAPR1302Title), Resources.ResourceManager,
            typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1302MessageFormat), Resources.ResourceManager,
            typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    /// <summary>
    /// Returns a set of descriptors for the diagnostics that this analyzer is capable of producing.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        WorkflowActivityRegistrationDescriptor
    ];


    /// <summary>
    /// Called once at session start to register actions in the analysis context.
    /// </summary>
    /// <param name="context"></param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeWorkflowActivityRegistration, SyntaxKind.InvocationExpression);
    }
    
    private static void AnalyzeWorkflowActivityRegistration(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
        {
            return;
        }

        if (memberAccessExpr.Name.Identifier.Text != "CallActivityAsync")
        {
            return;
        }

        var argumentList = invocationExpr.ArgumentList.Arguments;
        if (argumentList.Count == 0)
        {
            return;
        }

        var firstArgument = argumentList[0].Expression;
        if (firstArgument is not InvocationExpressionSyntax nameofInvocation)
        {
            return;
        }

        var activityName = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString().Trim('"');
        if (activityName == null)
        {
            return;
        }

        bool isRegistered = CheckIfActivityIsRegistered(activityName, context.SemanticModel);
        if (isRegistered)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(WorkflowActivityRegistrationDescriptor, firstArgument.GetLocation(), activityName);
        context.ReportDiagnostic(diagnostic);
    }
    
    private static bool CheckIfActivityIsRegistered(string activityName, SemanticModel semanticModel)
    {
        var methodInvocations = new List<InvocationExpressionSyntax>();
        foreach (var syntaxTree in semanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            methodInvocations.AddRange(root.DescendantNodes().OfType<InvocationExpressionSyntax>());
        }

        foreach (var invocation in methodInvocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName != "RegisterActivity")
            {
                continue;
            }

            if (memberAccess.Name is not GenericNameSyntax typeArgumentList ||
                typeArgumentList.TypeArgumentList.Arguments.Count <= 0)
            {
                continue;
            }

            if (typeArgumentList.TypeArgumentList.Arguments[0] is not IdentifierNameSyntax typeArgument)
            {
                continue;
            }

            if (typeArgument.Identifier.Text == activityName)
            {
                return true;
            }
        }

        return false;
    }
}
