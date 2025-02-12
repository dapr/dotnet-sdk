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
public class SubscriptionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor DiagnosticDescriptorMapSubscribeHandler = new(
        "DAPR2001",
        "Call MapSubscribeHandler",
        "Call app.MapSubscribeHandler to map endpoints for Dapr subscriptions",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DiagnosticDescriptorMapSubscribeHandler);

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

    private void AnalyzeMapSubscribeHandler(SyntaxNodeAnalysisContext context)
    {
        var withTopicInvocations = FindInvocations(context, "WithTopic");
        var methodsWithTopicAttribute = FindMethodsWithTopicAttribute(context);
        var invocationsWithTopicAttribute = FindInvocationsWithTopicAttribute(context);

        bool invokedByWebApplication = false;
        var mapSubscribeHandlerInvocation = FindInvocations(context, "MapSubscribeHandler")?.FirstOrDefault();

        if (mapSubscribeHandlerInvocation?.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
            if (symbolInfo.Symbol is ILocalSymbol localSymbol)
            {
                var type = localSymbol.Type;
                if (type.ToDisplayString() == "Microsoft.AspNetCore.Builder.WebApplication")
                {
                    invokedByWebApplication = true;
                }
            }
        }

        foreach (var withTopicInvocation in withTopicInvocations)
        {
            if (mapSubscribeHandlerInvocation == null || !invokedByWebApplication)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapSubscribeHandler, withTopicInvocation.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        foreach (var methodWithTopicAttribute in methodsWithTopicAttribute)
        {
            if (mapSubscribeHandlerInvocation == null || !invokedByWebApplication)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapSubscribeHandler, methodWithTopicAttribute.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        foreach (var invocationWithTopicAttribute in invocationsWithTopicAttribute)
        {
            if (mapSubscribeHandlerInvocation == null || !invokedByWebApplication)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapSubscribeHandler, invocationWithTopicAttribute.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private List<InvocationExpressionSyntax> FindInvocations(SyntaxNodeAnalysisContext context, string methodName)
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

    private List<MethodDeclarationSyntax> FindMethodsWithTopicAttribute(SyntaxNodeAnalysisContext context)
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

    private List<InvocationExpressionSyntax> FindInvocationsWithTopicAttribute(SyntaxNodeAnalysisContext context)
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
