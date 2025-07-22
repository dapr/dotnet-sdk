using Microsoft.AspNetCore.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Reflection;
using Dapr.Analyzers.Common;
using Microsoft.Extensions.Hosting;

namespace Dapr.Workflow.Analyzers.Test;

internal static class Utilities
{
    internal static ImmutableArray<DiagnosticAnalyzer> GetAnalyzers() =>
    [
        new WorkflowRegistrationAnalyzer(),
        new WorkflowActivityRegistrationAnalyzer()
    ];

    internal static IReadOnlyList<MetadataReference> GetReferences()
    {
        var metadataReferences = TestUtilities.GetAllReferencesNeededForType(typeof(WorkflowActivityRegistrationAnalyzer)).ToList();
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(WorkflowRegistrationAnalyzer)));
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(TimeSpan)));
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(Workflow<,>)));
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(WorkflowActivity<,>)));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(Task).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(WebApplication).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(DaprWorkflowClient).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(IApplicationBuilder).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(IHost).Assembly.Location));
        return metadataReferences;
    }
}
