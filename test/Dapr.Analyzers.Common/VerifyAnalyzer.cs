// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Dapr.Analyzers.Common;

internal class VerifyAnalyzer(IReadOnlyList<MetadataReference> metadataReferences)
{
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) => new(descriptor);

    public async Task VerifyAnalyzerAsync<TAnalyzer>(string source, params DiagnosticResult[] expected)
    where TAnalyzer : DiagnosticAnalyzer, new()
    {
        await VerifyAnalyzerAsync<TAnalyzer>(source, null, expected);
    }

    public async Task VerifyAnalyzerAsync<TAnalyzer>(string source, string? program, params DiagnosticResult[] expected)
    where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new Test<TAnalyzer> { TestCode = source };

#if NET8_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
#endif

        if (program != null)
        {
            test.TestState.Sources.Add(("Program.cs", program));
        }

        // var metadataReferences = TestUtilities.GetAllReferencesNeededForType(typeof(ActorRegistrationAnalyzer)).ToList();
        // metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(MappedActorHandlersAnalyzer)));
        // metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(PreferActorJsonSerializationAnalyzer)));
        // metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(TimerCallbackMethodPresentAnalyzer)));
        // metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(ActorsServiceCollectionExtensions)));
        // metadataReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        // metadataReferences.Add(MetadataReference.CreateFromFile(typeof(WebApplication).Assembly.Location));
        // metadataReferences.Add(MetadataReference.CreateFromFile(typeof(IHost).Assembly.Location));

        foreach (var reference in metadataReferences)
        {
            test.TestState.AdditionalReferences.Add(reference);
        }

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    private sealed class Test<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
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

