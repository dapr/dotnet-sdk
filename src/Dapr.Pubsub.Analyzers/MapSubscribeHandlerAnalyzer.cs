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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Pubsub.Analyzers;

/// <summary>
/// Analyzes the subscription methods to ensure proper usage of MapSubscribeHandler.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MapSubscribeHandlerAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor DiagnosticDescriptorMapSubscribeHandler = new(
        id: "DAPR1201",
        title: new LocalizableResourceString(nameof(Resources.DAPR1201Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR102MessageFormat), Resources.ResourceManager, typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptorMapSubscribeHandler];

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMapSubscribeHandler, SyntaxKind.CompilationUnit);
    }

    private static void AnalyzeMapSubscribeHandler(SyntaxNodeAnalysisContext context)
    {
        var withTopicInvocations = FindInvocations(context, "WithTopic");
        var methodsWithTopicAttribute = FindMethodsWithTopicAttribute(context);
        var invocationsWithTopicAttribute = FindInvocationsWithTopicAttribute(context);

        var mapSubscribeHandlerInvocation = FindInvocations(context, "MapSubscribeHandler")?.FirstOrDefault();

        foreach (var withTopicInvocation in withTopicInvocations)
        {
            if (mapSubscribeHandlerInvocation != null)
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapSubscribeHandler, withTopicInvocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var methodWithTopicAttribute in methodsWithTopicAttribute)
        {
            if (mapSubscribeHandlerInvocation != null)
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapSubscribeHandler, methodWithTopicAttribute.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var invocationWithTopicAttribute in invocationsWithTopicAttribute)
        {
            if (mapSubscribeHandlerInvocation != null)
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapSubscribeHandler, invocationWithTopicAttribute.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static IReadOnlyList<InvocationExpressionSyntax> FindInvocations(SyntaxNodeAnalysisContext context, string methodName)
    {
        var invocations = new List<InvocationExpressionSyntax>();

        foreach (var syntaxTree in context.SemanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            invocations.AddRange(root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.Text == methodName));
        }

        return invocations;
    }

    private static IReadOnlyList<MethodDeclarationSyntax> FindMethodsWithTopicAttribute(SyntaxNodeAnalysisContext context)
    {
        var methodsWithTopicAttribute = new List<MethodDeclarationSyntax>();

        foreach (var syntaxTree in context.SemanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            methodsWithTopicAttribute.AddRange(root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.AttributeLists
                    .SelectMany(attributeList => attributeList.Attributes)
                    .Any(attribute => attribute.Name.ToString() == "Topic" || attribute.Name.ToString().EndsWith(".Topic"))));
        }

        return methodsWithTopicAttribute;
    }

    private static List<InvocationExpressionSyntax> FindInvocationsWithTopicAttribute(SyntaxNodeAnalysisContext context)
    {
        var invocationsWithTopicAttributeParameter = new List<InvocationExpressionSyntax>();

        foreach (var syntaxTree in context.SemanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            invocationsWithTopicAttributeParameter.AddRange(root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.ArgumentList.Arguments
                    .Any(argument => argument.Expression is ParenthesizedLambdaExpressionSyntax parenthesizedLambdaExpression &&
                        parenthesizedLambdaExpression.AttributeLists
                            .SelectMany(attributeList => attributeList.Attributes)
                            .Any(attribute => attribute.Name.ToString() == "Topic" || attribute.Name.ToString().EndsWith(".Topic")))));
        }

        return invocationsWithTopicAttributeParameter;
    }
}
