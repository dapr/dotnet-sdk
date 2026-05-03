using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Validates that the input and output types used with Dapr workflow and workflow activity calls
/// match the declared generic input/output types of the target workflow/activity type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowTypeSafetyAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor InputTypeMismatchDescriptor = new(
        id: "DAPR1303",
        title: new LocalizableResourceString(nameof(Resources.DAPR1303Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1303MessageFormat), Resources.ResourceManager, typeof(Resources)),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor OutputTypeMismatchDescriptor = new(
        id: "DAPR1304",
        title: new LocalizableResourceString(nameof(Resources.DAPR1304Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1304MessageFormat), Resources.ResourceManager, typeof(Resources)),
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

        context.RegisterCompilationStartAction(static compilationStartContext =>
        {
            var compilation = compilationStartContext.Compilation;

            var daprWorkflowClientType = compilation.GetDaprWorkflowClientType();
            var iDaprWorkflowClientType = compilation.GetIDaprWorkflowClientType();
            var workflowContextType = compilation.GetWorkflowContextType();
            var workflowBaseType = compilation.GetWorkflowBaseType();
            var workflowActivityBaseType = compilation.GetWorkflowActivityBaseType();

            if (daprWorkflowClientType is null ||
                iDaprWorkflowClientType is null ||
                workflowContextType is null ||
                workflowBaseType is null ||
                workflowActivityBaseType is null)
            {
                return;
            }

            compilationStartContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(
                    operationContext,
                    daprWorkflowClientType,
                    iDaprWorkflowClientType,
                    workflowContextType,
                    workflowBaseType,
                    workflowActivityBaseType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol daprWorkflowClientType,
        INamedTypeSymbol iDaprWorkflowClientType,
        INamedTypeSymbol workflowContextType,
        INamedTypeSymbol workflowBaseType,
        INamedTypeSymbol workflowActivityBaseType)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var invocation = (IInvocationOperation)context.Operation;
        var targetMethod = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;

        if (IsScheduleNewWorkflowAsync(targetMethod, daprWorkflowClientType, iDaprWorkflowClientType))
        {
            AnalyzeWorkflowInput(invocation, context, workflowBaseType);
            return;
        }

        if (IsCallChildWorkflowAsync(targetMethod, workflowContextType))
        {
            AnalyzeWorkflowInput(invocation, context, workflowBaseType);
            AnalyzeWorkflowOutput(invocation, context, workflowBaseType);
            return;
        }

        if (IsCallActivityAsync(targetMethod, workflowContextType))
        {
            AnalyzeActivityInput(invocation, context, workflowActivityBaseType);
            AnalyzeActivityOutput(invocation, context, workflowActivityBaseType);
        }
    }

    private static bool IsScheduleNewWorkflowAsync(
        IMethodSymbol method,
        INamedTypeSymbol daprWorkflowClientType,
        INamedTypeSymbol iDaprWorkflowClientType) =>
        method.Name == "ScheduleNewWorkflowAsync" &&
        (SymbolEqualityComparer.Default.Equals(method.ContainingType, daprWorkflowClientType) ||
         SymbolEqualityComparer.Default.Equals(method.ContainingType, iDaprWorkflowClientType));

    private static bool IsCallChildWorkflowAsync(
        IMethodSymbol method,
        INamedTypeSymbol workflowContextType) =>
        method.Name == "CallChildWorkflowAsync" &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, workflowContextType);

    private static bool IsCallActivityAsync(
        IMethodSymbol method,
        INamedTypeSymbol workflowContextType) =>
        method.Name == "CallActivityAsync" &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, workflowContextType);

    private static void AnalyzeWorkflowInput(
        IInvocationOperation invocation,
        OperationAnalysisContext context,
        INamedTypeSymbol workflowBaseType)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!TryGetTargetTypeFromNameof(invocation, "name", context.CancellationToken, out var workflowType) &&
            !TryGetTargetTypeFromNameof(invocation, "workflowName", context.CancellationToken, out workflowType))
        {
            return;
        }

        if (!TryGetGenericArgumentsFromBaseType(workflowType, workflowBaseType, out var expectedInputType, out _))
        {
            return;
        }

        var inputArgument = GetArgument(invocation, "input");
        if (inputArgument is null ||
            !TryGetExpressionType(inputArgument.Value, out var actualInputType))
        {
            return;
        }

        if (IsCompatible(actualInputType, expectedInputType, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            InputTypeMismatchDescriptor,
            inputArgument.Syntax.GetLocation(),
            ToDisplayString(actualInputType),
            ToDisplayString(expectedInputType),
            "workflow",
            workflowType.Name));
    }

    private static void AnalyzeWorkflowOutput(
        IInvocationOperation invocation,
        OperationAnalysisContext context,
        INamedTypeSymbol workflowBaseType)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!TryGetTargetTypeFromNameof(invocation, "workflowName", context.CancellationToken, out var workflowType))
        {
            return;
        }

        if (!TryGetGenericArgumentsFromBaseType(workflowType, workflowBaseType, out _, out var declaredOutputType))
        {
            return;
        }

        if (!TryGetRequestedOutputType(invocation.TargetMethod, out var requestedOutputType))
        {
            return;
        }

        if (IsCompatible(declaredOutputType, requestedOutputType, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            OutputTypeMismatchDescriptor,
            GetInvocationNameLocation(invocation),
            ToDisplayString(requestedOutputType),
            ToDisplayString(declaredOutputType),
            "workflow",
            workflowType.Name));
    }

    private static void AnalyzeActivityInput(
        IInvocationOperation invocation,
        OperationAnalysisContext context,
        INamedTypeSymbol workflowActivityBaseType)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!TryGetTargetTypeFromNameof(invocation, "name", context.CancellationToken, out var activityType))
        {
            return;
        }

        if (!TryGetGenericArgumentsFromBaseType(activityType, workflowActivityBaseType, out var expectedInputType, out _))
        {
            return;
        }

        var inputArgument = GetArgument(invocation, "input");
        if (inputArgument is null ||
            !TryGetExpressionType(inputArgument.Value, out var actualInputType))
        {
            return;
        }

        if (IsCompatible(actualInputType, expectedInputType, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            InputTypeMismatchDescriptor,
            inputArgument.Syntax.GetLocation(),
            ToDisplayString(actualInputType),
            ToDisplayString(expectedInputType),
            "workflow activity",
            activityType.Name));
    }

    private static void AnalyzeActivityOutput(
        IInvocationOperation invocation,
        OperationAnalysisContext context,
        INamedTypeSymbol workflowActivityBaseType)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!TryGetTargetTypeFromNameof(invocation, "name", context.CancellationToken, out var activityType))
        {
            return;
        }

        if (!TryGetGenericArgumentsFromBaseType(activityType, workflowActivityBaseType, out _, out var declaredOutputType))
        {
            return;
        }

        if (!TryGetRequestedOutputType(invocation.TargetMethod, out var requestedOutputType))
        {
            return;
        }

        if (IsCompatible(declaredOutputType, requestedOutputType, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            OutputTypeMismatchDescriptor,
            GetInvocationNameLocation(invocation),
            ToDisplayString(requestedOutputType),
            ToDisplayString(declaredOutputType),
            "workflow activity",
            activityType.Name));
    }

    private static bool TryGetTargetTypeFromNameof(
        IInvocationOperation invocation,
        string parameterName,
        CancellationToken cancellationToken,
        out INamedTypeSymbol targetType)
    {
        cancellationToken.ThrowIfCancellationRequested();

        targetType = null!;
        var argument = GetArgument(invocation, parameterName);
        if (argument is null)
        {
            return false;
        }

        return TryGetTypeFromNameof(argument.Value, cancellationToken, out targetType);
    }

    private static bool TryGetTypeFromNameof(
        IOperation operation,
        CancellationToken cancellationToken,
        out INamedTypeSymbol targetType)
    {
        cancellationToken.ThrowIfCancellationRequested();

        targetType = null!;

        if (operation is not INameOfOperation nameOfOperation)
        {
            return false;
        }

        var argumentOperation = nameOfOperation.Argument;
        var semanticModel = argumentOperation.SemanticModel;
        if (semanticModel is null)
        {
            return false;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(argumentOperation.Syntax, cancellationToken).Symbol;
        if (symbolInfo is INamedTypeSymbol namedTypeFromSymbol)
        {
            targetType = namedTypeFromSymbol;
            return true;
        }

        if (argumentOperation.Type is INamedTypeSymbol namedType)
        {
            targetType = namedType;
            return true;
        }

        return false;
    }

    private static IArgumentOperation? GetArgument(
        IInvocationOperation invocation,
        string parameterName)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                return argument;
            }
        }

        return null;
    }

    private static bool TryGetGenericArgumentsFromBaseType(
        INamedTypeSymbol candidateType,
        INamedTypeSymbol genericBaseDefinition,
        out ITypeSymbol inputType,
        out ITypeSymbol outputType)
    {
        inputType = null!;
        outputType = null!;

        for (INamedTypeSymbol? current = candidateType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, genericBaseDefinition))
            {
                inputType = current.TypeArguments[0];
                outputType = current.TypeArguments[1];
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRequestedOutputType(
        IMethodSymbol methodSymbol,
        out ITypeSymbol requestedOutputType)
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
        IOperation operation,
        out ITypeSymbol expressionType)
    {
        expressionType = null!;

        if (operation.ConstantValue.HasValue && operation.ConstantValue.Value is null)
        {
            return false;
        }

        if (operation is IConversionOperation { IsImplicit: true, Operand: { } operand })
        {
            if (operand.ConstantValue.HasValue && operand.ConstantValue.Value is null)
            {
                return false;
            }

            expressionType = operand.Type!;
            return expressionType is not null;
        }

        expressionType = operation.Type!;
        return expressionType is not null;
    }

    private static bool IsCompatible(
        ITypeSymbol actualType,
        ITypeSymbol expectedType,
        Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(actualType, expectedType))
        {
            return true;
        }

        var conversion = compilation.ClassifyConversion(actualType, expectedType);
        return conversion.Exists && conversion.IsImplicit;
    }

    private static Location GetInvocationNameLocation(IInvocationOperation invocation)
    {
        return invocation.Syntax switch
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax
            {
                Expression: Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess
            } => memberAccess.Name.GetLocation(),
            _ => invocation.Syntax.GetLocation()
        };
    }

    private static string ToDisplayString(ITypeSymbol typeSymbol) =>
        typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
}
