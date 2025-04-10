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
public class WorkflowRegistrationAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor WorkflowDiagnosticDescriptor = new(
        id: "DAPR1301",
         title: new LocalizableResourceString(nameof(Resources.DAPR1301Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1301MessageFormat), Resources.ResourceManager, typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [WorkflowDiagnosticDescriptor];

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeWorkflowRegistration, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeWorkflowRegistration(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
        {
            return;
        }

        if (memberAccessExpr.Name.Identifier.Text != "ScheduleNewWorkflowAsync")
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

        var workflowName = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString().Trim('"');
        if (workflowName == null)
        {
            return;
        }

        bool isRegistered = CheckIfWorkflowIsRegistered(workflowName, context.SemanticModel);
        if (isRegistered)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(WorkflowDiagnosticDescriptor, firstArgument.GetLocation(), workflowName);
        context.ReportDiagnostic(diagnostic);
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
            
            if (methodName != "RegisterWorkflow")
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

            if (typeArgument.Identifier.Text == workflowName)
            {
                return true;
            }
        }

        return false;
    }
}
