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

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Dapr.Pubsub.Analyzers.Test;

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

        var metadataReferences = Utilities.GetAllReferencesNeededForType(typeof(SubscriptionAnalyzer)).ToList();
        //metadataReferences.AddRange(await test.ReferenceAssemblies.ResolveAsync(LanguageNames.CSharp, default));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        metadataReferences.AddRange(Utilities.GetAllReferencesNeededForType(typeof(WebApplication)));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(IHost).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(EndpointRouteBuilderExtensions).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(DaprEndpointConventionBuilderExtensions).Assembly.Location));

        foreach (var reference in metadataReferences)
        {
            test.TestState.AdditionalReferences.Add(reference);
        }

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    private class Test : CSharpAnalyzerTest<SubscriptionAnalyzer, DefaultVerifier>
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
