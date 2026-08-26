// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Workflow.Versioning.Generators.Test;

/// <summary>
/// In-process harness that runs <see cref="WorkflowSourceGenerator"/> over a snippet of
/// user source and returns the generated registry source plus generator diagnostics.
/// </summary>
internal static class GeneratorTestHarness
{
    public const string RegistryGeneratedFileName = "Dapr_Workflow_Versioning.g.cs";

    private static MetadataReference AbstractionsReference()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Dapr.Workflow.Abstractions.dll");
        return MetadataReference.CreateFromFile(path);
    }

    private static MetadataReference VersioningAbstractionsReference()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Dapr.Workflow.Versioning.Abstractions.dll");
        return MetadataReference.CreateFromFile(path);
    }

    /// <summary>
    /// Compiles <paramref name="userSource"/> and runs the source generator, returning the
    /// generated registry source (or an empty string when the generator emits nothing) and
    /// any diagnostics the generator reports.
    /// </summary>
    public static Task<(string GeneratedSource, Diagnostic[] Diagnostics)> RunAsync(string userSource)
    {
        // Build a complete reference set from the assemblies already loaded into the test host
        // (System.Runtime, System.Text.Json, Microsoft.Extensions.*, etc.) plus the abstractions
        // assemblies under test. This avoids the fragility of hand-picking individual framework refs.
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(MetadataReference? (a) => MetadataReference.CreateFromFile(a.Location))
            .Where(r => r is not null)
            .Cast<MetadataReference>()
            .ToList();
        references.Add(AbstractionsReference());
        references.Add(VersioningAbstractionsReference());

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(userSource) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new WorkflowSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var generated = string.Empty;
        foreach (var tree in runResult.GeneratedTrees.Where(tree => tree.FilePath.EndsWith(RegistryGeneratedFileName, StringComparison.Ordinal)))
        {
            generated = tree.ToString();
        }

        return Task.FromResult((generated, runResult.Diagnostics.ToArray()));
    }

    /// <summary>Asserts that no error-severity diagnostics were reported by the generator.</summary>
    public static void AssertNoErrorDiagnostics(Diagnostic[] diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0,
            $"Expected no error diagnostics, but got: {string.Join("; ", errors.Select(d => d.ToString()))}");
    }

    /// <summary>
    /// Re-parses the generated source and asserts it is syntactically valid C#. This catches
    /// generator defects such as an orphan <c>else</c> branch (the root cause of issue #1898).
    /// </summary>
    public static void AssertNoSyntaxErrors(string source)
    {
        if (string.IsNullOrEmpty(source))
            return;

        var tree = CSharpSyntaxTree.ParseText(source);
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0,
            $"Expected no syntax errors in generated source, but got: {string.Join("; ", errors.Select(d => d.ToString()))}");
    }
}
