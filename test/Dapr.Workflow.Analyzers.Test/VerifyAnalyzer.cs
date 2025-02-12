using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Dapr.Workflow.Analyzers.Test;

internal static class VerifyAnalyzer
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
        var test = new Test { TestCode = source };

#if NET6_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net60;
#elif NET7_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net70;
#elif NET8_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
#endif

        if (program != null)
        {
            test.TestState.Sources.Add(("Program.cs", program));
        }

        var metadataReferences = Utilities.GetAllReferencesNeededForType(typeof(WorkflowRegistrationAnalyzer)).ToList();
        metadataReferences.AddRange(Utilities.GetAllReferencesNeededForType(typeof(Workflow<,>)));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        foreach (var reference in metadataReferences)
        {
            test.TestState.AdditionalReferences.Add(reference);
        }

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    private class Test : CSharpAnalyzerTest<WorkflowRegistrationAnalyzer, DefaultVerifier>
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
}
