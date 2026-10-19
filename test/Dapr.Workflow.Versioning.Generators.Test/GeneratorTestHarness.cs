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
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;

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
        var result = Generate(userSource);
        return Task.FromResult((result.GeneratedSource, result.Diagnostics));
    }

    internal static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    internal static CSharpCompilation Compile(string source, IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).Cast<MetadataReference>()
            .Concat(new[]
            {
                AbstractionsReference(), VersioningAbstractionsReference(),
                MetadataReference.CreateFromFile(typeof(WorkflowVersioningRegistry).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DaprWorkflowClient).Assembly.Location)
            })
            .Concat(additionalReferences ?? [])
            .GroupBy(r => r.Display).Select(g => g.First());
        var compilation = CSharpCompilation.Create("GeneratedTests" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, ParseOptions)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        AssertNoErrorDiagnostics(compilation.GetDiagnostics().ToArray());
        return compilation;
    }

    internal static (Compilation Compilation, string GeneratedSource, Diagnostic[] Diagnostics) Generate(
        string source, IEnumerable<MetadataReference>? additionalReferences = null, bool scanReferences = false)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new WorkflowSourceGenerator().AsSourceGenerator()], parseOptions: ParseOptions,
            optionsProvider: new TestOptionsProvider(scanReferences));
        driver = driver.RunGeneratorsAndUpdateCompilation(Compile(source, additionalReferences), out var output, out var diagnostics);
        AssertNoErrorDiagnostics(diagnostics.ToArray());
        AssertNoErrorDiagnostics(output.GetDiagnostics().ToArray());
        var generated = driver.GetRunResult().GeneratedTrees
            .SingleOrDefault(t => t.FilePath.EndsWith(RegistryGeneratedFileName, StringComparison.Ordinal))?.ToString() ?? "";
        return (output, generated, diagnostics.ToArray());
    }

    internal static Assembly Load(Compilation compilation)
    {
        // Tests invoke registration explicitly so dynamically loaded assemblies do not
        // contaminate process-wide module-initializer registries in other test cases.
        foreach (var tree in compilation.SyntaxTrees.ToArray())
        {
            if (tree.ToString().Contains("[ModuleInitializer]", StringComparison.Ordinal))
                compilation = compilation.ReplaceSyntaxTree(tree,
                    CSharpSyntaxTree.ParseText(tree.ToString().Replace("[ModuleInitializer]", ""), ParseOptions));
        }
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }

    internal static void Register(Assembly assembly, WorkflowRuntimeOptions options, IServiceProvider services) =>
        assembly.GetType("Dapr.Workflow.Versioning.GeneratedWorkflowVersionRegistry")!
            .GetMethod("RegisterGeneratedWorkflows")!.Invoke(null, [options, services]);

    private sealed class TestOptionsProvider(bool scan) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(scan);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestOptions(false);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestOptions(false);
    }

    private sealed class TestOptions(bool scan) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = scan ? "true" : "false";
            return key == "build_property.DaprWorkflowVersioningScanReferences";
        }
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
