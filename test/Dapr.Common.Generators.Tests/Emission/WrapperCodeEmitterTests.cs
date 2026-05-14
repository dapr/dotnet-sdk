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
using Dapr.Common.Generators.Emission;
using Dapr.Common.Generators.Models;
using Dapr.Common.Generators.Tests.Helpers;

namespace Dapr.Common.Generators.Tests.Emission;

public sealed class WrapperCodeEmitterTests
{
    // -------------------------------------------------------------------------
    // Interface emission
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitInterface_ContainsInterfaceName()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant());
        var source = WrapperCodeEmitter.EmitInterface(groups!);

        Assert.Contains($"interface {WrapperCodeEmitter.InterfaceName}", source);
    }

    [Fact]
    public void EmitInterface_ContainsMethodSignature_ForEachGroup()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant());
        var source = WrapperCodeEmitter.EmitInterface(groups!);

        Assert.Contains("QuxAsync(", source);
    }

    [Fact]
    public void EmitInterface_UsesFullyQualifiedTypes()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant());
        var source = WrapperCodeEmitter.EmitInterface(groups!);

        // FQN prefix must appear for the request / response types
        Assert.Contains("global::", source);
    }

    // -------------------------------------------------------------------------
    // Class emission – PassThrough
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_PassThrough_UsesExpressionBody()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        // Expression-body delegate to _inner, no async keyword
        Assert.Contains("=> _inner.QuxAsync(request, options).ResponseAsync", source);
        Assert.DoesNotContain("async", source);
    }

    // -------------------------------------------------------------------------
    // Class emission – AutoCompatible (same types)
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_AutoCompatible_IdenticalTypes_ContainsCapabilityChecks()
    {
        var groups = Analyze(StubCompilation.WithIdenticalTypeVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        // Both variants must be checked
        Assert.Contains("SupportsMethodAsync(\"dapr.proto.runtime.v1.Dapr/Foo\"", source);
        Assert.Contains("SupportsMethodAsync(\"dapr.proto.runtime.v1.Dapr/FooAlpha1\"", source);
    }

    [Fact]
    public void EmitClass_AutoCompatible_IdenticalTypes_IsAsync()
    {
        var groups = Analyze(StubCompilation.WithIdenticalTypeVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("async global::System.Threading.Tasks.Task", source);
    }

    [Fact]
    public void EmitClass_AutoCompatible_IdenticalTypes_ThrowsFeatureNotAvailable()
    {
        var groups = Analyze(StubCompilation.WithIdenticalTypeVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("DaprFeatureNotAvailableException", source);
        Assert.Contains("\"Foo\"", source);
    }

    // -------------------------------------------------------------------------
    // Class emission – SchemaDivergent
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_SchemaDivergent_ThrowsNotSupportedException_ForFallback()
    {
        var groups = Analyze(StubCompilation.WithIncompatibleAlphaVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("NotSupportedException", source);
        Assert.Contains("DaprFeatureNotAvailableException", source);
    }

    [Fact]
    public void EmitClass_SchemaDivergent_MostRecentVariantIsCheckedFirst()
    {
        var groups = Analyze(StubCompilation.WithIncompatibleAlphaVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        // The capability-check strings should appear in order: Alpha2 before Alpha1.
        // For SchemaDivergent the fallback variant is not called (NotSupportedException is thrown),
        // so we look at the SupportsMethodAsync capability-check strings.
        var alpha2Pos = source.IndexOf("Dapr/BazAlpha2\"", StringComparison.Ordinal);
        var alpha1Pos = source.IndexOf("Dapr/BazAlpha1\"", StringComparison.Ordinal);

        Assert.True(alpha2Pos >= 0, "SupportsMethodAsync check for BazAlpha2 should be present");
        Assert.True(alpha1Pos >= 0, "SupportsMethodAsync check for BazAlpha1 should be present");
        Assert.True(alpha2Pos < alpha1Pos, "Alpha2 (most recent) capability check should appear before Alpha1");
    }

    // -------------------------------------------------------------------------
    // Class emission – Unimplemented fallback guard
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_AutoCompatible_MostRecentVariant_ContainsUnimplementedCatch()
    {
        var groups = Analyze(StubCompilation.WithIdenticalTypeVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        // The most-recent variant should be wrapped in a try-catch for StatusCode.Unimplemented
        // so that a runtime which defines but doesn't implement the method falls through to Alpha1.
        Assert.Contains("StatusCode.Unimplemented", source);
        Assert.Contains("catch (global::Grpc.Core.RpcException __implEx)", source);
    }

    [Fact]
    public void EmitClass_SchemaDivergent_MostRecentVariant_ContainsUnimplementedCatch()
    {
        var groups = Analyze(StubCompilation.WithIncompatibleAlphaVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("StatusCode.Unimplemented", source);
        Assert.Contains("catch (global::Grpc.Core.RpcException __implEx)", source);
    }

    [Fact]
    public void EmitClass_PassThrough_DoesNotContainUnimplementedCatch()
    {
        // PassThrough methods have only one variant — no fallback, no UNIMPLEMENTED guard needed.
        var groups = Analyze(StubCompilation.WithSingleStableVariant());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.DoesNotContain("StatusCode.Unimplemented", source);
    }

    [Fact]
    public void EmitClass_AutoCompatible_MostRecentVariant_ContainsUnknownProxyErrorCatch()
    {
        // Older Dapr runtimes return StatusCode.Unknown with a proxy-routing error message when
        // they receive a gRPC method they don't recognise. This must also trigger fallback.
        var groups = Analyze(StubCompilation.WithIdenticalTypeVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("StatusCode.Unknown", source);
        Assert.Contains("dapr-callee-app-id or dapr-app-id not found", source);
    }

    [Fact]
    public void EmitClass_SchemaDivergent_MostRecentVariant_ContainsUnknownProxyErrorCatch()
    {
        // Same proxy-routing fallback must also be present for SchemaDivergent methods.
        var groups = Analyze(StubCompilation.WithIncompatibleAlphaVariants());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("StatusCode.Unknown", source);
        Assert.Contains("dapr-callee-app-id or dapr-app-id not found", source);
    }

    // -------------------------------------------------------------------------
    // Class emission – constructor and fields
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_ContainsConstructorWithInnerAndCapabilities()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains($"public {WrapperCodeEmitter.ClassName}(", source);
        Assert.Contains("IDaprRuntimeCapabilities", source);
        Assert.Contains("DaprClient inner", source);
    }

    [Fact]
    public void EmitClass_IsPartial()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("partial class", source);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IReadOnlyList<MethodGroup>? Analyze(Microsoft.CodeAnalysis.Compilation compilation)
        => DaprClientAnalyzer.AnalyzeCompilation(compilation);
}
