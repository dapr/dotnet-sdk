using Microsoft.CodeAnalysis;

namespace Dapr.Workflow.Analyzers;

internal static class CompilationExtensions
{
    private const string DaprWorkflowClientMetadataName = "Dapr.Workflow.DaprWorkflowClient";
    private const string IDaprWorkflowClientMetadataName = "Dapr.Workflow.IDaprWorkflowClient";
    private const string WorkflowContextMetadataName = "Dapr.Workflow.WorkflowContext";
    private const string WorkflowBaseMetadataName = "Dapr.Workflow.Workflow`2";
    private const string WorkflowActivityBaseMetadataName = "Dapr.Workflow.WorkflowActivity`2";

    internal static INamedTypeSymbol? GetDaprWorkflowClientType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(DaprWorkflowClientMetadataName);

    internal static INamedTypeSymbol? GetIDaprWorkflowClientType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(IDaprWorkflowClientMetadataName);

    internal static INamedTypeSymbol? GetWorkflowContextType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(WorkflowContextMetadataName);

    internal static INamedTypeSymbol? GetWorkflowBaseType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(WorkflowBaseMetadataName);

    internal static INamedTypeSymbol? GetWorkflowActivityBaseType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(WorkflowActivityBaseMetadataName);
}
