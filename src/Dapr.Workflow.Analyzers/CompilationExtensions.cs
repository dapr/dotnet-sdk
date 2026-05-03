using Microsoft.CodeAnalysis;

namespace Dapr.Workflow.Analyzers;

internal static class CompilationExtensions
{
    private const string DaprWorkflowClientMetadataName = "Dapr.Workflow.DaprWorkflowClient";
    private const string IDaprWorkflowClientMetadataName = "Dapr.Workflow.IDaprWorkflowClient";
    private const string WorkflowContextMetadataName = "Dapr.Workflow.WorkflowContext";
    private const string WorkflowBaseMetadataName = "Dapr.Workflow.Workflow`2";
    private const string WorkflowActivityBaseMetadataName = "Dapr.Workflow.WorkflowActivity`2";
    private const string WorkflowAbstractionsAssemblyName = "Dapr.Workflow.Abstractions";

    internal static INamedTypeSymbol? GetDaprWorkflowClientType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(DaprWorkflowClientMetadataName);

    internal static INamedTypeSymbol? GetIDaprWorkflowClientType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(IDaprWorkflowClientMetadataName);

    internal static INamedTypeSymbol? GetWorkflowContextType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(WorkflowContextMetadataName);

    /// <summary>
    /// Gets the <c>Dapr.Workflow.Workflow&lt;,&gt;</c> base type, verifying it originates from the
    /// <c>Dapr.Workflow.Abstractions</c> assembly to avoid false positives against user-defined
    /// types that share the same fully-qualified name.
    /// </summary>
    internal static INamedTypeSymbol? GetWorkflowBaseType(this Compilation compilation)
    {
        var type = compilation.GetTypeByMetadataName(WorkflowBaseMetadataName);
        if (type is not null &&
            !string.Equals(type.ContainingAssembly.Name, WorkflowAbstractionsAssemblyName, StringComparison.Ordinal))
        {
            return null;
        }

        return type;
    }

    internal static INamedTypeSymbol? GetWorkflowActivityBaseType(this Compilation compilation) =>
        compilation.GetTypeByMetadataName(WorkflowActivityBaseMetadataName);
}
