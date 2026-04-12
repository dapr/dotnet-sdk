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
/// Analyzes whether a Workflow implementation attempts to use constructor-based dependency
/// injection, which is not supported by the Dapr workflow runtime because workflow code must
/// be deterministic and is replayed multiple times.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowDependencyInjectionAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor WorkflowDependencyInjectionDescriptor = new(
        id: "DAPR1305",
        title: new LocalizableResourceString(nameof(Resources.DAPR1305Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1305MessageFormat), Resources.ResourceManager, typeof(Resources)),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the diagnostics supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        WorkflowDependencyInjectionDescriptor
    ];

    /// <summary>
    /// Initializes analyzer actions.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStartContext =>
        {
            var workflowBaseType = compilationStartContext.Compilation.GetWorkflowBaseType();
            if (workflowBaseType is null)
            {
                return;
            }

            compilationStartContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeClassDeclaration(nodeContext, workflowBaseType),
                SyntaxKind.ClassDeclaration);
        });
    }

    private static void AnalyzeClassDeclaration(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol workflowBaseType)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken) is not INamedTypeSymbol classSymbol)
        {
            return;
        }

        if (!DerivesFromWorkflow(classSymbol, workflowBaseType))
        {
            return;
        }

        foreach (var constructor in classDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                var parameterSymbol = context.SemanticModel
                    .GetDeclaredSymbol(parameter, context.CancellationToken);

                var typeName = parameterSymbol?.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                    ?? parameter.Type?.ToString()
                    ?? "unknown";

                var paramName = parameter.Identifier.Text;

                context.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDependencyInjectionDescriptor,
                    parameter.GetLocation(),
                    classSymbol.Name,
                    paramName,
                    typeName));
            }
        }
    }

    private static bool DerivesFromWorkflow(INamedTypeSymbol classSymbol, INamedTypeSymbol workflowBaseType)
    {
        for (var current = classSymbol.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, workflowBaseType))
            {
                return true;
            }
        }

        return false;
    }
}
