// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Dapr.Common.Generators.Emission;
using Dapr.Common.Generators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Common.Generators.Tests;

public sealed class DaprVersionAwareGeneratorTests
{
    // -------------------------------------------------------------------------
    // Opt-in flag behaviour
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_WithFlagTrue_EmitsTwoSourceFiles()
    {
        var result = RunGenerator("true", StubCompilation.WithSingleStableVariant());

        Assert.Equal(2, result.GeneratedTrees.Length);
    }

    [Fact]
    public void Generator_WithFlagFalse_EmitsNothing()
    {
        var result = RunGenerator("false", StubCompilation.WithSingleStableVariant());

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Generator_WithFlagMissing_EmitsNothing()
    {
        var result = RunGenerator(flagValue: null, StubCompilation.WithSingleStableVariant());

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Generator_WithFlagTrueUppercase_EmitsTwoSourceFiles()
    {
        // The flag check uses OrdinalIgnoreCase, so "TRUE" must also trigger emission.
        var result = RunGenerator("TRUE", StubCompilation.WithSingleStableVariant());

        Assert.Equal(2, result.GeneratedTrees.Length);
    }

    // -------------------------------------------------------------------------
    // Output file names
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_WithFlagTrue_OutputFileNamesMatchEmitterConstants()
    {
        var result = RunGenerator("true", StubCompilation.WithSingleStableVariant());

        var hints = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.HintName)
            .ToList();

        Assert.Contains($"{WrapperCodeEmitter.InterfaceName}.g.cs", hints);
        Assert.Contains($"{WrapperCodeEmitter.ClassName}.g.cs", hints);
    }

    // -------------------------------------------------------------------------
    // Empty / missing DaprClient
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_WithFlagTrue_EmptyDaprClient_EmitsNothing()
    {
        // DaprClient type exists but has no async-unary methods → groups is null → no output.
        var compilation = StubCompilation.Create(daprClientMethods: "");
        var result = RunGenerator("true", compilation);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Generator_WithFlagTrue_NoDaprClientType_EmitsNothing()
    {
        // A bare compilation without any DaprClient type at all → no output.
        var compilation = CSharpCompilation.Create(
            assemblyName: "Empty",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = RunGenerator("true", compilation);

        Assert.Empty(result.GeneratedTrees);
    }

    // -------------------------------------------------------------------------
    // Source content sanity checks
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_WithFlagTrue_InterfaceSourceContainsInterfaceName()
    {
        var result = RunGenerator("true", StubCompilation.WithSingleStableVariant());

        var interfaceSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == $"{WrapperCodeEmitter.InterfaceName}.g.cs")
            .SourceText
            .ToString();

        Assert.Contains(WrapperCodeEmitter.InterfaceName, interfaceSource);
    }

    [Fact]
    public void Generator_WithFlagTrue_ClassSourceContainsClassName()
    {
        var result = RunGenerator("true", StubCompilation.WithSingleStableVariant());

        var classSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == $"{WrapperCodeEmitter.ClassName}.g.cs")
            .SourceText
            .ToString();

        Assert.Contains(WrapperCodeEmitter.ClassName, classSource);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static GeneratorDriverRunResult RunGenerator(string? flagValue, Compilation compilation)
    {
        var generator = new DaprVersionAwareGenerator();
        var options = flagValue is not null
            ? new MockAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            {
                ["build_property.IsDaprSdkProject"] = flagValue
            })
            : new MockAnalyzerConfigOptionsProvider();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            optionsProvider: options);

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }
}
