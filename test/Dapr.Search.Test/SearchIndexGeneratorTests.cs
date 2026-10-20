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
using Dapr.Search.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Search.Test;

public sealed class SearchIndexGeneratorTests
{
    [Theory]
    [InlineData("public partial interface IProducts : IDaprSearchIndex<string>;", "AddProducts")]
    [InlineData("internal partial interface Products : IDaprSearchIndex<int>;", "AddProducts")]
    [InlineData("public partial interface @interface : IDaprSearchIndex<string?>;", "Addinterface")]
    [InlineData("public partial interface I : IDaprSearchIndex<System.Collections.Generic.Dictionary<string, int>>;", "AddI")]
    public void GeneratesCompilableGlobalInterfaces(string declaration, string method)
    {
        var (result, compilation) = Generate($$"""
            using Dapr.Search;
            [SearchIndex("searchstore", "products")]
            {{declaration}}
            """);
        var generated = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains(method + "(", generated);
        Assert.Contains("private const string StoreName = \"searchstore\";", generated);
        Assert.Contains("private const string IndexName = \"products\";", generated);
        AssertCompiles(result, compilation);
    }

    [Fact]
    public void HandlesIndirectInheritancePartialDeclarationsAndDuplicateSimpleNames()
    {
        var (result, compilation) = Generate("""
            using Dapr.Search;
            namespace First
            {
                public interface IBase : IDaprSearchIndex<string> { }
                [SearchIndex("one", "first")]
                public partial interface IProducts : IBase { }
                public partial interface IProducts { }
            }
            namespace Second
            {
                [SearchIndex("two", "second")]
                internal partial interface IProducts : IDaprSearchIndex<int> { }
            }
            """);
        Assert.Equal(2, result.GeneratedTrees.Length);
        AssertCompiles(result, compilation);
    }

    [Fact]
    public void EscapesComponentAndIndexStringLiterals()
    {
        var (result, compilation) = Generate("""
            using Dapr.Search;
            namespace @namespace;
            [SearchIndex("quote\" slash\\ line\n", "index\t\u0022")]
            public partial interface IProducts : IDaprSearchIndex<string> { }
            """);
        AssertCompiles(result, compilation);
        var generated = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains("quote\\\" slash\\\\ line\\n", generated);
        Assert.Contains("index\\t\\\"", generated);
    }

    [Theory]
    [InlineData("""[SearchIndex("s", "i")] public interface IInvalid : IDaprSearchIndex<string> { }""", "DAPR1801")]
    [InlineData("""[SearchIndex("s", "i")] public partial interface IInvalid<T> : IDaprSearchIndex<T> { }""", "DAPR1801")]
    [InlineData("""class Outer { [SearchIndex("s", "i")] public partial interface IInvalid : IDaprSearchIndex<string> { } }""", "DAPR1801")]
    [InlineData("""[SearchIndex("s", "i")] file partial interface IInvalid : IDaprSearchIndex<string> { }""", "DAPR1801")]
    [InlineData("""[SearchIndex("", "i")] public partial interface IInvalid : IDaprSearchIndex<string> { }""", "DAPR1802")]
    [InlineData("""[SearchIndex("s", " ")] public partial interface IInvalid : IDaprSearchIndex<string> { }""", "DAPR1802")]
    [InlineData("""[SearchIndex(null!, "i")] public partial interface IInvalid : IDaprSearchIndex<string> { }""", "DAPR1802")]
    [InlineData("""[SearchIndex("s", "i")] public partial interface IInvalid { }""", "DAPR1803")]
    [InlineData("""[SearchIndex("s", "i")] public partial interface IInvalid : IDaprSearchIndex<int>, IDaprSearchIndex<string> { }""", "DAPR1803")]
    [InlineData("""[SearchIndex("s", "i")] public partial interface IInvalid : IDaprSearchIndex<string> { void Extra(); }""", "DAPR1803")]
    [InlineData("""interface IExtra { string Name { get; } } [SearchIndex("s", "i")] partial interface IInvalid : IDaprSearchIndex<string>, IExtra { }""", "DAPR1803")]
    public void InvalidDeclarationsProduceActionableDiagnosticsWithoutSource(string declaration, string diagnosticId)
    {
        var (result, _) = Generate("using Dapr.Search;\n" + declaration);
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
            using Dapr.Search;
            partial interface IUnannotated : IDaprSearchIndex<string> { }
            namespace Other
            {
                class SearchIndexAttribute : System.Attribute { }
                [SearchIndex] partial interface IUnrelated { }
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
                typeof(DaprSearchClient).Assembly.Location,
                typeof(DaprSearchClientBuilder).Assembly.Location,
                typeof(IDaprClient).Assembly.Location,
                typeof(ServiceDescriptor).Assembly.Location
            }).Distinct(StringComparer.OrdinalIgnoreCase);
        var compilation = CSharpCompilation.Create("SearchGeneratorConsumer",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12))],
            paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SearchIndexSourceGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        return (driver.GetRunResult(), updated);
    }
}
