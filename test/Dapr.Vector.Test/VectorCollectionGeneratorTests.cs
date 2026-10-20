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

using Dapr.Common;
using Dapr.Vector.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Vector.Test;

public sealed class VectorCollectionGeneratorTests
{
    [Theory]
    [InlineData("public partial interface IProducts : IDaprVectorCollection<string>;", "AddProducts")]
    [InlineData("internal partial interface Products : IDaprVectorCollection<int>;", "AddProducts")]
    [InlineData("public partial interface @interface : IDaprVectorCollection<string?>;", "Addinterface")]
    [InlineData("public partial interface I : IDaprVectorCollection<System.Collections.Generic.Dictionary<string, int>>;", "AddI")]
    public void GeneratesCompilableGlobalInterfaces(string declaration, string method)
    {
        var (result, compilation) = Generate($$"""
            using Dapr.Vector;
            [VectorCollection("vectorstore", "products")]
            {{declaration}}
            """);
        var generated = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains(method + "(", generated);
        Assert.Contains("private const string StoreName = \"vectorstore\";", generated);
        Assert.Contains("private const string CollectionName = \"products\";", generated);
        AssertCompiles(result, compilation);
    }

    [Fact]
    public void HandlesIndirectInheritancePartialDeclarationsAndDuplicateSimpleNames()
    {
        var (result, compilation) = Generate("""
            using Dapr.Vector;
            namespace First
            {
                public interface IBase : IDaprVectorCollection<string> { }
                [VectorCollection("one", "first")]
                public partial interface IProducts : IBase { }
                public partial interface IProducts { }
            }
            namespace Second
            {
                [VectorCollection("two", "second")]
                internal partial interface IProducts : IDaprVectorCollection<int> { }
            }
            """);
        Assert.Equal(2, result.GeneratedTrees.Length);
        AssertCompiles(result, compilation);
    }

    [Fact]
    public void EscapesComponentAndIndexStringLiterals()
    {
        var (result, compilation) = Generate("""
            using Dapr.Vector;
            namespace @namespace;
            [VectorCollection("quote\" slash\\ line\n", "index\t\u0022")]
            public partial interface IProducts : IDaprVectorCollection<string> { }
            """);
        AssertCompiles(result, compilation);
        var generated = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains("quote\\\" slash\\\\ line\\n", generated);
        Assert.Contains("index\\t\\\"", generated);
    }

    [Theory]
    [InlineData("""[VectorCollection("s", "i")] public interface IInvalid : IDaprVectorCollection<string> { }""", "DAPR1901")]
    [InlineData("""[VectorCollection("s", "i")] public partial interface IInvalid<T> : IDaprVectorCollection<T> { }""", "DAPR1901")]
    [InlineData("""class Outer { [VectorCollection("s", "i")] public partial interface IInvalid : IDaprVectorCollection<string> { } }""", "DAPR1901")]
    [InlineData("""[VectorCollection("s", "i")] file partial interface IInvalid : IDaprVectorCollection<string> { }""", "DAPR1901")]
    [InlineData("""[VectorCollection("", "i")] public partial interface IInvalid : IDaprVectorCollection<string> { }""", "DAPR1902")]
    [InlineData("""[VectorCollection("s", " ")] public partial interface IInvalid : IDaprVectorCollection<string> { }""", "DAPR1902")]
    [InlineData("""[VectorCollection(null!, "i")] public partial interface IInvalid : IDaprVectorCollection<string> { }""", "DAPR1902")]
    [InlineData("""[VectorCollection("s", "i")] public partial interface IInvalid { }""", "DAPR1903")]
    [InlineData("""[VectorCollection("s", "i")] public partial interface IInvalid : IDaprVectorCollection<int>, IDaprVectorCollection<string> { }""", "DAPR1903")]
    [InlineData("""[VectorCollection("s", "i")] public partial interface IInvalid : IDaprVectorCollection<string> { void Extra(); }""", "DAPR1903")]
    [InlineData("""interface IExtra { string Name { get; } } [VectorCollection("s", "i")] partial interface IInvalid : IDaprVectorCollection<string>, IExtra { }""", "DAPR1903")]
    public void InvalidDeclarationsProduceActionableDiagnosticsWithoutSource(string declaration, string diagnosticId)
    {
        var (result, _) = Generate("using Dapr.Vector;\n" + declaration);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(diagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void DoesNotGenerateForUnannotatedOrUnrelatedAttributes()
    {
        var (result, compilation) = Generate("""
            using Dapr.Vector;
            partial interface IUnannotated : IDaprVectorCollection<string> { }
            namespace Other
            {
                class VectorCollectionAttribute : System.Attribute { }
                [VectorCollection] partial interface IUnrelated { }
            }
            """);
        Assert.Empty(result.GeneratedTrees);
        AssertCompiles(result, compilation);
    }

    private static void AssertCompiles(GeneratorDriverRunResult result, Compilation compilation)
    {
        Assert.Empty(result.Diagnostics);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(d => d.ToString())));
    }

    private static (GeneratorDriverRunResult Result, Compilation Compilation) Generate(string source)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(new[]
            {
                typeof(DaprVectorClient).Assembly.Location,
                typeof(DaprVectorClientBuilder).Assembly.Location,
                typeof(IDaprClient).Assembly.Location,
                typeof(ServiceDescriptor).Assembly.Location
            }).Distinct(StringComparer.OrdinalIgnoreCase);
        var compilation = CSharpCompilation.Create("VectorGeneratorConsumer",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12))],
            paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new VectorCollectionSourceGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        return (driver.GetRunResult(), updated);
    }
}
