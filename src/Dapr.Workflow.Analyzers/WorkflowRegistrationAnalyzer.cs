// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Analyzes whether or not workflow activities are registered.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowRegistrationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor WorkflowDiagnosticDescriptor = new(
        "DAPR1001",
        "Workflow not registered",
        "The workflow class '{0}' is not registered",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor WorkflowActivityDiagnosticDescriptor = new(
        "DAPR1002",
        "Workflow activity not registered",
        "The workflow activity class '{0}' is not registered",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);


    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(WorkflowDiagnosticDescriptor, WorkflowActivityDiagnosticDescriptor);

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeWorkflowRegistration, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeWorkflowActivityRegistration, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeWorkflowRegistration(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
            return;

        if (memberAccessExpr.Name.Identifier.Text != "ScheduleNewWorkflowAsync")
            return;

        var argumentList = invocationExpr.ArgumentList.Arguments;
        if (argumentList.Count == 0)
            return;

        var firstArgument = argumentList[0].Expression;
        if (firstArgument is InvocationExpressionSyntax nameofInvocation)
        {
            var workflowName = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString().Trim('"');
            if (workflowName != null)
            {
                bool isRegistered = CheckIfWorkflowIsRegistered(workflowName, context.SemanticModel);
                if (!isRegistered)
                {
                    var diagnostic = Diagnostic.Create(WorkflowDiagnosticDescriptor, firstArgument.GetLocation(), workflowName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private void AnalyzeWorkflowActivityRegistration(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
            return;

        if (memberAccessExpr.Name.Identifier.Text != "CallActivityAsync") 
            return; 
        
        var argumentList = invocationExpr.ArgumentList.Arguments; 
        if (argumentList.Count == 0) 
            return;

        var firstArgument = argumentList[0].Expression;
        if (firstArgument is InvocationExpressionSyntax nameofInvocation)
        { 
            var activityName = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString().Trim('"');
            if (activityName != null) 
            { 
                bool isRegistered = CheckIfActivityIsRegistered(activityName, context.SemanticModel); 
                if (!isRegistered) 
                {
                    var diagnostic = Diagnostic.Create(WorkflowActivityDiagnosticDescriptor, firstArgument.GetLocation(), activityName);
                    context.ReportDiagnostic(diagnostic);
                } 
            } 
        }
    }

    private static bool CheckIfWorkflowIsRegistered(string workflowName, SemanticModel semanticModel)
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
            if (methodName == "RegisterWorkflow")
            {
                if (memberAccess.Name is GenericNameSyntax typeArgumentList && typeArgumentList.TypeArgumentList.Arguments.Count > 0)
                {
                    if (typeArgumentList.TypeArgumentList.Arguments[0] is IdentifierNameSyntax typeArgument)
                    {
                        if (typeArgument.Identifier.Text == workflowName)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
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
            if (methodName == "RegisterActivity")
            {
                if (memberAccess.Name is GenericNameSyntax typeArgumentList && typeArgumentList.TypeArgumentList.Arguments.Count > 0)
                {
                    if (typeArgumentList.TypeArgumentList.Arguments[0] is IdentifierNameSyntax typeArgument)
                    {                        
                        if (typeArgument.Identifier.Text == activityName)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
