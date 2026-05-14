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

using Dapr.Common.Generators.Analysis;
using Dapr.Common.Generators.Models;
using Dapr.Common.Generators.Tests.Helpers;

namespace Dapr.Common.Generators.Tests.Analysis;

public sealed class DaprClientAnalyzerTests
{
    // -------------------------------------------------------------------------
    // SplitMaturitySuffix – pure string tests
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("ScheduleJob",       "ScheduleJob", "")]
    [InlineData("ScheduleJobAlpha1", "ScheduleJob", "Alpha1")]
    [InlineData("ScheduleJobAlpha2", "ScheduleJob", "Alpha2")]
    [InlineData("ScheduleJobBeta1",  "ScheduleJob", "Beta1")]
    [InlineData("ScheduleJobRC1",    "ScheduleJob", "RC1")]
    [InlineData("ListJobs",          "ListJobs",    "")]
    [InlineData("ConverseAlpha1",    "Converse",    "Alpha1")]
    [InlineData("ConverseAlpha2",    "Converse",    "Alpha2")]
    [InlineData("Alpha",             "Alpha",       "")]      // no digit after — not a suffix
    [InlineData("GetAlphaFoo",       "GetAlphaFoo", "")]      // "Alpha" not at end
    public void SplitMaturitySuffix_ReturnsExpected(string grpcName, string expectedBase, string expectedSuffix)
    {
        var (baseName, suffix) = DaprClientAnalyzer.SplitMaturitySuffix(grpcName);
        Assert.Equal(expectedBase, baseName);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Theory]
    [InlineData("FooRC1",    "RC1")]
    [InlineData("FooRC2",    "RC2")]
    [InlineData("FooBeta1",  "Beta1")]
    [InlineData("FooBeta2",  "Beta2")]
    [InlineData("FooAlpha1", "Alpha1")]
    [InlineData("FooAlpha2", "Alpha2")]
    [InlineData("Foo",       "")]
    public void SplitMaturitySuffix_SuffixRoundTrips(string grpcName, string expectedSuffix)
    {
        var (_, actualSuffix) = DaprClientAnalyzer.SplitMaturitySuffix(grpcName);
        Assert.Equal(expectedSuffix, actualSuffix);
    }

    // -------------------------------------------------------------------------
    // AnalyzeCompilation – compilation-based tests
    // -------------------------------------------------------------------------

    [Fact]
    public void StubCompilation_TypesResolve()
    {
        // Sanity check: all three types the analyzer needs must resolve in the stub compilation.
        var compilation = StubCompilation.WithSingleStableVariant();

        var callOptions = compilation.GetTypeByMetadataName("Grpc.Core.CallOptions");
        var asyncCall   = compilation.GetTypeByMetadataName("Grpc.Core.AsyncUnaryCall`1");
        var daprClient  = compilation.GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.Dapr+DaprClient");

        Assert.NotNull(callOptions);
        Assert.NotNull(asyncCall);
        Assert.NotNull(daprClient);

        // DaprClient must have at least one method
        Assert.NotEmpty(daprClient!.GetMembers().OfType<Microsoft.CodeAnalysis.IMethodSymbol>());
    }

    [Fact]
    public void StubCompilation_MethodParameterTypesMatch()
    {
        // Verify that the method parameter type IS the same symbol as the one
        // returned by GetTypeByMetadataName (SymbolEqualityComparer must agree).
        var compilation = StubCompilation.WithSingleStableVariant();

        var callOptionsType  = compilation.GetTypeByMetadataName("Grpc.Core.CallOptions")!;
        var asyncCallType    = compilation.GetTypeByMetadataName("Grpc.Core.AsyncUnaryCall`1")!;
        var daprClientType   = compilation.GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.Dapr+DaprClient")!;

        var methods = daprClientType.GetMembers()
            .OfType<Microsoft.CodeAnalysis.IMethodSymbol>()
            .Where(m => m.Name.EndsWith("Async"))
            .ToList();

        Assert.NotEmpty(methods);
        var method = methods[0];
        Assert.Equal(2, method.Parameters.Length);

        // Second parameter must be identical to the resolved CallOptions symbol
        Assert.True(
            Microsoft.CodeAnalysis.SymbolEqualityComparer.Default.Equals(
                method.Parameters[1].Type, callOptionsType),
            $"Parameter type '{method.Parameters[1].Type}' should equal '{callOptionsType}'");

        // Return type's original definition must match AsyncUnaryCall<T>
        var returnNamed = (Microsoft.CodeAnalysis.INamedTypeSymbol)method.ReturnType;
        Assert.True(
            Microsoft.CodeAnalysis.SymbolEqualityComparer.Default.Equals(
                returnNamed.OriginalDefinition, asyncCallType),
            $"Return type '{returnNamed}' OriginalDefinition should equal '{asyncCallType}'");
    }

    [Fact]
    public void AnalyzeCompilation_ReturnsNull_WhenDaprClientNotFound()
    {
        // Compilation with no DaprClient at all
        var compilation = StubCompilation.Create(daprClientMethods: "");

        // Remove the outer class by using a compilation with only grpc stubs
        var emptyCompilation = StubCompilation.Create(
            daprClientMethods: "",
            extraTypes: "");

        // The stub DOES define a DaprClient, so AnalyzeCompilation should return null
        // when there are no matching methods (no Async unary methods).
        var result = DaprClientAnalyzer.AnalyzeCompilation(emptyCompilation);
        Assert.Null(result);
    }

    [Fact]
    public void AnalyzeCompilation_PassThrough_SingleStableVariant()
    {
        var compilation = StubCompilation.WithSingleStableVariant();
        var groups = DaprClientAnalyzer.AnalyzeCompilation(compilation);

        Assert.NotNull(groups);
        var group = Assert.Single(groups);
        Assert.Equal("Qux", group.BaseName);
        Assert.Equal(MethodClassification.PassThrough, group.Classification);
        Assert.Empty(group.Fallbacks);
        Assert.Equal("QuxAsync", group.MostRecent.CSharpMethodName);
        Assert.Equal(MaturityLevel.Stable, group.MostRecent.Level);
    }

    [Fact]
    public void AnalyzeCompilation_AutoCompatible_IdenticalTypes()
    {
        var compilation = StubCompilation.WithIdenticalTypeVariants();
        var groups = DaprClientAnalyzer.AnalyzeCompilation(compilation);

        Assert.NotNull(groups);
        var group = Assert.Single(groups);
        Assert.Equal("Foo", group.BaseName);
        Assert.Equal(MethodClassification.AutoCompatible, group.Classification);
        Assert.Single(group.Fallbacks);

        // Stable variant is most recent
        Assert.Equal(MaturityLevel.Stable, group.MostRecent.Level);
        Assert.Equal("FooAsync", group.MostRecent.CSharpMethodName);

        // Alpha1 is the single fallback
        Assert.Equal(MaturityLevel.Alpha, group.Fallbacks[0].Level);
        Assert.Equal("FooAlpha1Async", group.Fallbacks[0].CSharpMethodName);
    }

    [Fact]
    public void AnalyzeCompilation_AutoCompatible_DifferentButCompatibleTypes()
    {
        var compilation = StubCompilation.WithCompatibleDifferentTypes();
        var groups = DaprClientAnalyzer.AnalyzeCompilation(compilation);

        Assert.NotNull(groups);
        var group = Assert.Single(groups);
        Assert.Equal("Bar", group.BaseName);
        Assert.Equal(MethodClassification.AutoCompatible, group.Classification);
    }

    [Fact]
    public void AnalyzeCompilation_SchemaDivergent_IncompatibleAlphaTypes()
    {
        var compilation = StubCompilation.WithIncompatibleAlphaVariants();
        var groups = DaprClientAnalyzer.AnalyzeCompilation(compilation);

        Assert.NotNull(groups);
        var group = Assert.Single(groups);
        Assert.Equal("Baz", group.BaseName);
        Assert.Equal(MethodClassification.SchemaDivergent, group.Classification);

        // Alpha2 (higher number) is the most recent
        Assert.Equal(MaturityLevel.Alpha, group.MostRecent.Level);
        Assert.Equal(2, group.MostRecent.LevelNumber);
        Assert.Equal("BazAlpha2Async", group.MostRecent.CSharpMethodName);
    }

    [Fact]
    public void AnalyzeCompilation_AutoCompatible_ObsoleteAlphaVariantIncludedAsFallback()
    {
        // Regression test: the real Dapr gRPC stubs mark Alpha1 methods [Obsolete] once the
        // stable API is promoted. The analyzer must NOT skip those — they are the fallback targets
        // for older runtimes that only support the alpha API.
        var compilation = StubCompilation.WithObsoleteAlphaVariant();
        var groups = DaprClientAnalyzer.AnalyzeCompilation(compilation);

        Assert.NotNull(groups);
        var group = Assert.Single(groups);
        Assert.Equal("Corf", group.BaseName);
        Assert.Equal(MethodClassification.AutoCompatible, group.Classification);
        Assert.Single(group.Fallbacks);
        Assert.Equal("CorfAsync", group.MostRecent.CSharpMethodName);
        Assert.Equal("CorfAlpha1Async", group.Fallbacks[0].CSharpMethodName);
    }

    [Fact]
    public void AnalyzeCompilation_MultipleGroups_ReturnsOneGroupPerBaseName()
    {
        var compilation = StubCompilation.Create(
            daprClientMethods: """
                public global::Grpc.Core.AsyncUnaryCall<Q1Response> Method1Async(Q1Request r, global::Grpc.Core.CallOptions o) { return null; }
                public global::Grpc.Core.AsyncUnaryCall<Q2Response> Method2Async(Q2Request r, global::Grpc.Core.CallOptions o) { return null; }
                """,
            extraTypes: """
                public class Q1Request  { }
                public class Q1Response { }
                public class Q2Request  { }
                public class Q2Response { }
                """);

        var groups = DaprClientAnalyzer.AnalyzeCompilation(compilation);

        Assert.NotNull(groups);
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.BaseName == "Method1");
        Assert.Contains(groups, g => g.BaseName == "Method2");
    }

    // -------------------------------------------------------------------------
    // AreFieldsCompatible
    // -------------------------------------------------------------------------

    [Fact]
    public void AreFieldsCompatible_SameType_ReturnsTrue()
    {
        var compilation = StubCompilation.WithIdenticalTypeVariants();
        var fooRequest = compilation.GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.FooRequest")!;

        Assert.True(DaprClientAnalyzer.AreFieldsCompatible(fooRequest, fooRequest));
    }

    [Fact]
    public void AreFieldsCompatible_SameFieldNames_ReturnsTrue()
    {
        var compilation = StubCompilation.WithCompatibleDifferentTypes();
        var newer = compilation.GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.BarRequest")!;
        var older = compilation.GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.BarRequestAlpha1")!;

        Assert.True(DaprClientAnalyzer.AreFieldsCompatible(newer, older));
    }

    [Fact]
    public void AreFieldsCompatible_MissingFieldInNewer_ReturnsFalse()
    {
        var compilation = StubCompilation.WithIncompatibleAlphaVariants();
        var newer = compilation.GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.BazRequestAlpha2")!;
        var older = compilation.GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.BazRequestAlpha1")!;

        Assert.False(DaprClientAnalyzer.AreFieldsCompatible(newer, older));
    }
}
