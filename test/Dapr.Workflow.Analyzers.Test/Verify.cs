using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Dapr.Workflow.Analyzers.Test;

internal static class Verify
{
    public static DiagnosticResult Diagnostic(string diagnosticId, DiagnosticSeverity diagnosticSeverity) 
    { 
        return new DiagnosticResult(diagnosticId, diagnosticSeverity);
    }

    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected) 
    { 
        await VerifyAnalyzerAsync(source, null, expected);
    }

    public static async Task VerifyAnalyzerAsync(string source, string? program, params DiagnosticResult[] expected)
    {
        var test = new Test { TestCode = source, ReferenceAssemblies = ReferenceAssemblies.Net.Net60 };

        if (program != null)
        {
            test.TestState.Sources.Add(("Program.cs", program));
        }

        var metadataReferences = GetAllReferencesNeededForType(typeof(WorkflowActivityAnalyzer)).ToList();
        metadataReferences.AddRange(GetAllReferencesNeededForType(typeof(Workflow<,>)));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        foreach (var reference in metadataReferences)
        {
            test.TestState.AdditionalReferences.Add(reference);
        }

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    private class Test : CSharpAnalyzerTest<WorkflowActivityAnalyzer, DefaultVerifier>
    { 
        public Test() 
        { 
            SolutionTransforms.Add((solution, projectId) => 
            { 
                var compilationOptions = solution.GetProject(projectId)!.CompilationOptions!;
                solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);
                return solution;
            }); 
        } 
    }

    private static MetadataReference[] GetAllReferencesNeededForType(Type type)
    {
        var files = GetAllAssemblyFilesNeededForType(type);

        return files.Select(x => MetadataReference.CreateFromFile(x)).Cast<MetadataReference>().ToArray();
    }

    private static ImmutableArray<string> GetAllAssemblyFilesNeededForType(Type type)
    {
        return type.Assembly.GetReferencedAssemblies()
            .Select(x => Assembly.Load(x.FullName))
            .Append(type.Assembly)
            .Select(x => x.Location)
            .ToImmutableArray();
    }
}
