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
    // Class emission – obsolete-warning suppression
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_ContainsPragmaDisableObsoleteWarning()
    {
        // The generated class file must suppress CS0618 because it intentionally calls
        // [Obsolete]-tagged alpha/beta stubs as fallback targets for older runtimes.
        var groups = Analyze(StubCompilation.WithObsoleteAlphaVariant());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("#pragma warning disable CS0612, CS0618", source);
    }

    [Fact]
    public void EmitClass_ObsoleteAlpha_GeneratesAutoCompatibleFallback()
    {
        // When the Alpha1 variant is [Obsolete], the class must still emit the full
        // capability-check + try/catch + fallback chain (not a simple PassThrough).
        var groups = Analyze(StubCompilation.WithObsoleteAlphaVariant());
        var source = WrapperCodeEmitter.EmitClass(groups!);

        Assert.Contains("SupportsMethodAsync(\"dapr.proto.runtime.v1.Dapr/Corf\"", source);
        Assert.Contains("SupportsMethodAsync(\"dapr.proto.runtime.v1.Dapr/CorfAlpha1\"", source);
        Assert.Contains("CorfAlpha1Async", source);
        Assert.Contains("async global::System.Threading.Tasks.Task", source);
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
    // Emit() – top-level tuple
    // -------------------------------------------------------------------------

    [Fact]
    public void Emit_ReturnsTuple_BothSourcesNonEmpty()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var (interfaceSource, classSource) = WrapperCodeEmitter.Emit(groups);

        Assert.NotEmpty(interfaceSource);
        Assert.NotEmpty(classSource);
    }

    [Fact]
    public void Emit_InterfaceSourceMatchesEmitInterface()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var (interfaceSource, _) = WrapperCodeEmitter.Emit(groups);

        Assert.Equal(WrapperCodeEmitter.EmitInterface(groups), interfaceSource);
    }

    [Fact]
    public void Emit_ClassSourceMatchesEmitClass()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var (_, classSource) = WrapperCodeEmitter.Emit(groups);

        Assert.Equal(WrapperCodeEmitter.EmitClass(groups), classSource);
    }

    // -------------------------------------------------------------------------
    // File header / namespace
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitInterface_ContainsAutoGeneratedComment()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var source = WrapperCodeEmitter.EmitInterface(groups);

        Assert.Contains("// <auto-generated/>", source);
    }

    [Fact]
    public void EmitClass_ContainsAutoGeneratedComment()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("// <auto-generated/>", source);
    }

    [Fact]
    public void EmitClass_ContainsNullableEnable()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("#nullable enable", source);
    }

    [Fact]
    public void EmitClass_ContainsNamespace()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("namespace Dapr.Common;", source);
    }

    [Fact]
    public void EmitInterface_ContainsNamespace()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var source = WrapperCodeEmitter.EmitInterface(groups);

        Assert.Contains("namespace Dapr.Common;", source);
    }

    // -------------------------------------------------------------------------
    // Class fields and constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_ContainsInnerField()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("DaprClient _inner", source);
    }

    [Fact]
    public void EmitClass_ContainsCapabilitiesField()
    {
        var groups = Analyze(StubCompilation.WithSingleStableVariant())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("IDaprRuntimeCapabilities _capabilities", source);
    }

    // -------------------------------------------------------------------------
    // AutoCompatible – request/response type conversion (different types)
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_AutoCompatible_DifferentTypes_EmitsNewFallbackRequestObject()
    {
        // BarRequest → BarRequestAlpha1 conversion should create a new instance.
        var groups = Analyze(StubCompilation.WithCompatibleDifferentTypes())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("new global::Dapr.Client.Autogen.Grpc.v1.BarRequestAlpha1()", source);
    }

    [Fact]
    public void EmitClass_AutoCompatible_DifferentTypes_EmitsScalarPropertyCopyForRequest()
    {
        // The "Name" field from BarRequest should be copied to __fallbackRequest.Name.
        var groups = Analyze(StubCompilation.WithCompatibleDifferentTypes())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("__fallbackRequest.Name = request.Name;", source);
    }

    [Fact]
    public void EmitClass_AutoCompatible_DifferentTypes_EmitsNewResponseObject()
    {
        // BarResponseAlpha1 → BarResponse conversion should create a new BarResponse instance.
        var groups = Analyze(StubCompilation.WithCompatibleDifferentTypes())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("new global::Dapr.Client.Autogen.Grpc.v1.BarResponse()", source);
    }

    [Fact]
    public void EmitClass_AutoCompatible_DifferentTypes_EmitsScalarPropertyCopyForResponse()
    {
        // The "Result" field from BarResponseAlpha1 should be copied to __convertedResponse.Result.
        var groups = Analyze(StubCompilation.WithCompatibleDifferentTypes())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("__convertedResponse.Result = __fallbackResponse.Result;", source);
    }

    [Fact]
    public void EmitClass_AutoCompatible_DifferentTypes_UsesFullFallbackBlock()
    {
        // When types differ the compact one-liner form must NOT be used.
        var groups = Analyze(StubCompilation.WithCompatibleDifferentTypes())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        // Compact form would be "return await _inner.BarAlpha1Async(request, options)..."
        Assert.DoesNotContain("BarAlpha1Async(request, options)", source);
        // Instead the fallback request variable is used
        Assert.Contains("BarAlpha1Async(__fallbackRequest, options)", source);
    }

    // -------------------------------------------------------------------------
    // EmitRequestConversion – same-type inner short-circuit
    // (sameRequest=true, sameResponse=false → else block runs, but request is not converted)
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_SameRequestDifferentResponse_EmitsFallbackRequestEqualsRequest()
    {
        // When mostRecent and fallback share the same request type, EmitRequestConversion
        // must emit the short-circuit form `var __fallbackRequest = request;` rather than
        // creating a new object.
        var groups = Analyze(StubCompilation.WithSameRequestDifferentResponse())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("var __fallbackRequest = request;", source);
    }

    [Fact]
    public void EmitClass_SameRequestDifferentResponse_DoesNotCreateNewRequestObject()
    {
        var groups = Analyze(StubCompilation.WithSameRequestDifferentResponse())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        // No `new XyzRequest*()` should appear — the original `request` is reused.
        Assert.DoesNotContain("new global::Dapr.Client.Autogen.Grpc.v1.XyzRequest", source);
    }

    [Fact]
    public void EmitClass_SameRequestDifferentResponse_EmitsNewResponseObjectAndPropertyCopy()
    {
        // The response types differ, so EmitResponseConversion must create a new XyzResponse
        // and copy the Result property.
        var groups = Analyze(StubCompilation.WithSameRequestDifferentResponse())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("new global::Dapr.Client.Autogen.Grpc.v1.XyzResponse()", source);
        Assert.Contains("__convertedResponse.Result = __fallbackResponse.Result;", source);
        Assert.Contains("return __convertedResponse;", source);
    }

    // -------------------------------------------------------------------------
    // EmitResponseConversion – same-type inner short-circuit
    // (sameRequest=false, sameResponse=true → else block runs, but response is not converted)
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_DifferentRequestSameResponse_EmitsNewFallbackRequestObject()
    {
        // Request types differ, so EmitRequestConversion must create a new AbcRequestAlpha1.
        var groups = Analyze(StubCompilation.WithDifferentRequestSameResponse())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("new global::Dapr.Client.Autogen.Grpc.v1.AbcRequestAlpha1()", source);
        Assert.Contains("__fallbackRequest.Name = request.Name;", source);
    }

    [Fact]
    public void EmitClass_DifferentRequestSameResponse_EmitsReturnFallbackResponse()
    {
        // When mostRecent and fallback share the same response type, EmitResponseConversion
        // must emit the short-circuit `return __fallbackResponse;` rather than a conversion.
        var groups = Analyze(StubCompilation.WithDifferentRequestSameResponse())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("return __fallbackResponse;", source);
    }

    [Fact]
    public void EmitClass_DifferentRequestSameResponse_DoesNotCreateNewResponseObject()
    {
        var groups = Analyze(StubCompilation.WithDifferentRequestSameResponse())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        // No `new AbcResponse()` should appear — the fallback response is returned directly.
        Assert.DoesNotContain("new global::Dapr.Client.Autogen.Grpc.v1.AbcResponse()", source);
    }

    // -------------------------------------------------------------------------
    // AutoCompatible – same types (compact one-liner)
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_AutoCompatible_SameTypes_UsesCompactOneLiner()
    {
        // Identical types → no new object, no property copy; uses request directly.
        var groups = Analyze(StubCompilation.WithIdenticalTypeVariants())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        // The compact form passes request directly (no __fallbackRequest)
        Assert.Contains("FooAlpha1Async(request, options).ResponseAsync", source);
        Assert.DoesNotContain("__fallbackRequest", source);
    }

    // -------------------------------------------------------------------------
    // EmitPropertyCopy – collection fields
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_WithCollectionFields_EmitsAddRangeForRepeatedField()
    {
        var groups = Analyze(StubCompilation.WithCollectionFields())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("__fallbackRequest.Tags.AddRange(request.Tags);", source);
    }

    [Fact]
    public void EmitClass_WithCollectionFields_EmitsForeachForMapField()
    {
        var groups = Analyze(StubCompilation.WithCollectionFields())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("foreach (var __kvp in request.Labels)", source);
        Assert.Contains("__fallbackRequest.Labels[__kvp.Key] = __kvp.Value;", source);
    }

    [Fact]
    public void EmitClass_WithCollectionFields_EmitsScalarAssignmentForWritableField()
    {
        var groups = Analyze(StubCompilation.WithCollectionFields())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("__fallbackRequest.Name = request.Name;", source);
    }

    [Fact]
    public void EmitClass_WithCollectionFields_SkipsReadOnlyNonCollectionField()
    {
        // ReadOnlyCount { get; } is neither RepeatedField nor MapField, and has no setter.
        // EmitPropertyCopy must skip it — no assignment to ReadOnlyCount should appear.
        var groups = Analyze(StubCompilation.WithCollectionFields())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.DoesNotContain("ReadOnlyCount = ", source);
        Assert.DoesNotContain(".ReadOnlyCount =", source);
    }

    // -------------------------------------------------------------------------
    // Multiple fallbacks
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitClass_MultipleFallbacks_EmitsAllCapabilityChecks()
    {
        var groups = Analyze(StubCompilation.WithMultipleFallbacks())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        Assert.Contains("SupportsMethodAsync(\"dapr.proto.runtime.v1.Dapr/Grault\"", source);
        Assert.Contains("SupportsMethodAsync(\"dapr.proto.runtime.v1.Dapr/GraultAlpha2\"", source);
        Assert.Contains("SupportsMethodAsync(\"dapr.proto.runtime.v1.Dapr/GraultAlpha1\"", source);
    }

    [Fact]
    public void EmitClass_MultipleFallbacks_MostRecentCheckedFirst()
    {
        var groups = Analyze(StubCompilation.WithMultipleFallbacks())!;
        var source = WrapperCodeEmitter.EmitClass(groups);

        // The stable "Grault" check must appear before Alpha2, which must appear before Alpha1.
        var stablePos = source.IndexOf("Dapr/Grault\"", StringComparison.Ordinal);
        var alpha2Pos = source.IndexOf("Dapr/GraultAlpha2\"", StringComparison.Ordinal);
        var alpha1Pos = source.IndexOf("Dapr/GraultAlpha1\"", StringComparison.Ordinal);

        Assert.True(stablePos < alpha2Pos, "Stable check should appear before Alpha2 check");
        Assert.True(alpha2Pos < alpha1Pos, "Alpha2 check should appear before Alpha1 check");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IReadOnlyList<MethodGroup>? Analyze(Microsoft.CodeAnalysis.Compilation compilation)
        => DaprClientAnalyzer.AnalyzeCompilation(compilation);
}
