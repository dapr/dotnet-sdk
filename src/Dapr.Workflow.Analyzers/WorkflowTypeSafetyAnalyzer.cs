using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Validate that the input and output types to and from a workflow and workflow activity match on either side of the operation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowTypeSafetyAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor InputTypeMismatchDescriptor = new(
        id: "DAPR1303",
        title: new LocalizableResourceString(nameof(Resources.DAPR1303Title), Resources.ResourceManager,
            typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1303MessageFormat), Resources.ResourceManager,
            typeof(Resources)),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor OutputTypeMismatchDescriptor = new(
        id: "DAPR1304",
        title: new LocalizableResourceString(nameof(Resources.DAPR1304Title), Resources.ResourceManager,
            typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1304MessageFormat), Resources.ResourceManager,
            typeof(Resources)),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the diagnostics supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        InputTypeMismatchDescriptor,
        OutputTypeMismatchDescriptor
    ];

    /// <summary>
    /// Initializes analyzer actions.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            methodSymbol)
        {
            return;
        }

        methodSymbol = methodSymbol.ReducedFrom ?? methodSymbol;

        switch (methodSymbol.Name)
        {
            case "ScheduleNewWorkflowAsync":
                AnalyzeWorkflowInput(invocation, methodSymbol, context);
                break;
            case "CallChildWorkflowAsync":
                AnalyzeWorkflowInput(invocation, methodSymbol, context);
                AnalyzeWorkflowOutput(invocation, methodSymbol, context);
                break;
            case "CallActivityAsync":
                AnalyzeActivityInput(invocation, methodSymbol, context);
                AnalyzeActivityOutput(invocation, methodSymbol, context);
                break;
        }
    }

    private static void AnalyzeWorkflowInput(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SyntaxNodeAnalysisContext context)
    {
        if (!TryGetTargetType(invocation, methodSymbol, context, "name", "workflowName", out var workflowType))
        {
            return;
        }

        if (!TryGetWorkflowTypeArguments(workflowType, out var expectedInputType, out _))
        {
            return;
        }

        var inputArgument = GetArgument(invocation, methodSymbol, "input");
        if (inputArgument is null || !TryGetExpressionType(context.SemanticModel, inputArgument.Expression,
                context.CancellationToken, out var actualInputType))
        {
            return;
        }

        if (IsCompatible(actualInputType, expectedInputType, context.SemanticModel.Compilation))
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            InputTypeMismatchDescriptor,
            inputArgument.GetLocation(),
            ToDisplayString(actualInputType),
            ToDisplayString(expectedInputType),
            "workflow",
            workflowType.Name);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeWorkflowOutput(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SyntaxNodeAnalysisContext context)
    {
        if (!TryGetTargetType(invocation, methodSymbol, context, "workflowName", out var workflowType))
        {
            return;
        }

        if (!TryGetWorkflowTypeArguments(workflowType, out _, out var declaredOutputType))
        {
            return;
        }

        if (!TryGetRequestedOutputType(methodSymbol, out var requestedOutputType))
        {
            return;
        }

        if (IsCompatible(declaredOutputType, requestedOutputType, context.SemanticModel.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            OutputTypeMismatchDescriptor,
            GetOutputDiagnosticLocation(invocation),
            ToDisplayString(requestedOutputType),
            ToDisplayString(declaredOutputType),
            "workflow",
            workflowType.Name));
    }

    private static void AnalyzeActivityInput(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SyntaxNodeAnalysisContext context)
    {
        if (!TryGetTargetType(invocation, methodSymbol, context, "name", out var activityType))
        {
            return;
        }

        if (!TryGetActivityTypeArguments(activityType, out var expectedInputType, out _))
        {
            return;
        }

        var inputArgument = GetArgument(invocation, methodSymbol, "input");
        if (inputArgument is null || !TryGetExpressionType(context.SemanticModel, inputArgument.Expression,
                context.CancellationToken, out var actualInputType))
        {
            return;
        }

        if (IsCompatible(actualInputType, expectedInputType, context.SemanticModel.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            InputTypeMismatchDescriptor,
            inputArgument.GetLocation(),
            ToDisplayString(actualInputType),
            ToDisplayString(expectedInputType),
            "workflow activity",
            activityType.Name));
    }

    private static void AnalyzeActivityOutput(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SyntaxNodeAnalysisContext context)
    {
        if (!TryGetTargetType(invocation, methodSymbol, context, "name", out var activityType))
        {
            return;
        }

        if (!TryGetActivityTypeArguments(activityType, out _, out var declaredOutputType))
        {
            return;
        }

        if (!TryGetRequestedOutputType(methodSymbol, out var requestedOutputType))
        {
            return;
        }

        if (IsCompatible(declaredOutputType, requestedOutputType, context.SemanticModel.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            OutputTypeMismatchDescriptor,
            GetOutputDiagnosticLocation(invocation),
            ToDisplayString(requestedOutputType),
            ToDisplayString(declaredOutputType),
            "workflow activity",
            activityType.Name));
    }

    private static bool TryGetTargetType(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SyntaxNodeAnalysisContext context,
        string parameterName,
        out INamedTypeSymbol targetType)
    {
        targetType = null!;
        var argument = GetArgument(invocation, methodSymbol, parameterName);
        if (argument is null)
        {
            return false;
        }

        return TryGetTypeFromNameof(argument.Expression, context.SemanticModel, context.CancellationToken,
            out targetType);
    }

    private static bool TryGetTargetType(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SyntaxNodeAnalysisContext context,
        string firstParameterName,
        string secondParameterName,
        out INamedTypeSymbol targetType)
    {
        targetType = null!;
        return TryGetTargetType(invocation, methodSymbol, context, firstParameterName, out targetType) ||
               TryGetTargetType(invocation, methodSymbol, context, secondParameterName, out targetType);
    }

    private static bool TryGetTypeFromNameof(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out INamedTypeSymbol targetType)
    {
        targetType = null!;

        if (expression is not InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } nameofIdentifier
            } nameofInvocation)
        {
            return false;
        }

        _ = nameofIdentifier;
        var targetExpression = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (targetExpression is null)
        {
            return false;
        }

        var resolvedType = semanticModel.GetSymbolInfo(targetExpression, cancellationToken).Symbol as INamedTypeSymbol;
        if (resolvedType is null)
        {
            return false;
        }

        targetType = resolvedType;
        return true;
    }

    private static ArgumentSyntax? GetArgument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        string parameterName)
    {
        var positionalIndex = 0;
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.NameColon is not null)
            {
                if (argument.NameColon.Name.Identifier.Text == parameterName)
                {
                    return argument;
                }

                continue;
            }

            if (positionalIndex >= methodSymbol.Parameters.Length)
            {
                return null;
            }

            if (methodSymbol.Parameters[positionalIndex].Name == parameterName)
            {
                return argument;
            }

            positionalIndex++;
        }

        return null;
    }

    private static bool TryGetWorkflowTypeArguments(
        INamedTypeSymbol workflowType,
        out ITypeSymbol inputType,
        out ITypeSymbol outputType)
    {
        inputType = null!;
        outputType = null!;
        var genericBase = FindGenericBaseType(workflowType, "Dapr.Workflow.Workflow`2");
        if (genericBase is null)
        {
            return false;
        }

        inputType = genericBase.TypeArguments[0];
        outputType = genericBase.TypeArguments[1];
        return true;
    }

    private static bool TryGetActivityTypeArguments(
        INamedTypeSymbol activityType,
        out ITypeSymbol inputType,
        out ITypeSymbol outputType)
    {
        inputType = null!;
        outputType = null!;
        var genericBase = FindGenericBaseType(activityType, "Dapr.Workflow.WorkflowActivity`2");
        if (genericBase is null)
        {
            return false;
        }

        inputType = genericBase.TypeArguments[0];
        outputType = genericBase.TypeArguments[1];
        return true;
    }

    private static INamedTypeSymbol? FindGenericBaseType(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if ($"{current.OriginalDefinition.ContainingNamespace.ToDisplayString()}.{current.OriginalDefinition.MetadataName}" ==
                metadataName)
            {
                return current;
            }
        }

        return null;
    }

    private static bool TryGetRequestedOutputType(IMethodSymbol methodSymbol, out ITypeSymbol requestedOutputType)
    {
        requestedOutputType = null!;
        if (!methodSymbol.IsGenericMethod || methodSymbol.TypeArguments.Length != 1)
        {
            return false;
        }

        requestedOutputType = methodSymbol.TypeArguments[0];
        return true;
    }

    private static bool TryGetExpressionType(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        CancellationToken cancellationToken,
        out ITypeSymbol expressionType)
    {
        expressionType = null!;
        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return false;
        }

        var typeInfo = semanticModel.GetTypeInfo(expression, cancellationToken);
        expressionType = typeInfo.Type ?? typeInfo.ConvertedType!;
        return expressionType is not null;
    }

    private static bool IsCompatible(ITypeSymbol actualType, ITypeSymbol expectedType, Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(actualType, expectedType))
        {
            return true;
        }

        var conversion = compilation.ClassifyConversion(actualType, expectedType);
        return conversion.Exists && conversion.IsImplicit;
    }

    private static Location GetOutputDiagnosticLocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.GetLocation();
        }

        return invocation.GetLocation();
    }

    private static string ToDisplayString(ITypeSymbol typeSymbol) =>
        typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
}
