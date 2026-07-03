using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.State;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Dapr.Actors.Next.Analyzers.Test;

internal static class AnalyzerTest
{
    internal static DiagnosticResult Diagnostic(string id) => new(id, DiagnosticSeverity.Warning);

    internal static DiagnosticResult Info(string id) => new(id, DiagnosticSeverity.Info);

    internal static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new AnalyzerHarness { TestCode = source };
        AddReferences(test);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    internal static Task VerifyAnalyzerWithBaselineAsync(string source, string shippedBaseline, params DiagnosticResult[] expected)
    {
        var test = new AnalyzerHarness { TestCode = source };
        AddReferences(test);
        test.TestState.AdditionalFiles.Add(("DaprActorsNext.Shipped.txt", shippedBaseline));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    internal static Task VerifyCodeFixAsync(string source, string fixedSource, string diagnosticId, int codeActionIndex = 0, int? numberOfIterations = null)
    {
        var test = new CodeFixHarness
        {
            TestCode = source,
            FixedCode = fixedSource,
            CodeActionIndex = codeActionIndex,
        };
        if (numberOfIterations is not null)
        {
            test.NumberOfIncrementalIterations = numberOfIterations.Value;
        }

        AddReferences(test);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    internal static Task VerifyCodeFixWithBaselineAsync(string source, string fixedSource, string shippedBaseline, string fixedBaseline, string diagnosticId, int codeActionIndex = 0, int? numberOfIterations = null)
    {
        var test = new CodeFixHarness
        {
            TestCode = source,
            FixedCode = fixedSource,
            CodeActionIndex = codeActionIndex,
        };
        if (numberOfIterations is not null)
        {
            test.NumberOfIncrementalIterations = numberOfIterations.Value;
        }

        AddReferences(test);
        test.TestState.AdditionalFiles.Add(("DaprActorsNext.Shipped.txt", shippedBaseline));
        test.FixedState.AdditionalFiles.Add(("DaprActorsNext.Shipped.txt", fixedBaseline));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static void AddReferences<TVerifier>(Microsoft.CodeAnalysis.Testing.AnalyzerTest<TVerifier> test)
        where TVerifier : IVerifier, new()
    {
#if NET8_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
#elif NET10_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net100;
#endif
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Actor).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(DaprActorAttribute).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IActorTurnFilter).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IActorStateUpcaster<,>).Assembly.Location));
    }

    private static void AddReferences<TVerifier>(Microsoft.CodeAnalysis.Testing.CodeFixTest<TVerifier> test)
        where TVerifier : IVerifier, new()
    {
#if NET8_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
#elif NET10_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net100;
#endif
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Actor).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(DaprActorAttribute).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IActorTurnFilter).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IActorStateUpcaster<,>).Assembly.Location));
    }

    private sealed class AnalyzerHarness : CSharpAnalyzerTest<ActorsNextAnalyzer, DefaultVerifier>;

    private sealed class CodeFixHarness : CSharpCodeFixTest<ActorsNextAnalyzer, ActorsNextCodeFixProvider, DefaultVerifier>;
}
