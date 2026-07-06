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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Common.Generators.Tests.Helpers;

/// <summary>
/// Creates minimal in-memory <see cref="Compilation"/> objects whose type structure
/// mirrors the Dapr gRPC proto-generated code, allowing unit tests to exercise the
/// analyser and emitter without referencing external assemblies.
/// </summary>
/// <remarks>
/// Important: method stubs that are placed inside <c>namespace Dapr.Client.Autogen.Grpc.v1</c>
/// MUST use <c>global::Grpc.Core.CallOptions</c> / <c>global::Grpc.Core.AsyncUnaryCall&lt;T&gt;</c>.
/// Without <c>global::</c>, the C# compiler resolves <c>Grpc</c> relative to the enclosing
/// namespace tree and lands on <c>Dapr.Client.Autogen.Grpc.Core</c> instead of the top-level
/// <c>Grpc.Core</c> stub, causing type-equality checks in the analyser to fail.
/// </remarks>
internal static class StubCompilation
{
    /// <summary>
    /// Minimal Grpc.Core stubs: CallOptions and AsyncUnaryCall&lt;T&gt;.
    /// Kept deliberately simple — the analyzer only checks type identity.
    /// </summary>
    private const string GrpcCoreStubs = """
        namespace Grpc.Core
        {
            public struct CallOptions { }
            public class AsyncUnaryCall<TResponse> { }
        }
        """;

    /// <summary>
    /// Creates a compilation whose DaprClient contains the supplied method source.
    /// Extra types are emitted at file scope (outside the Dapr namespace) so that
    /// their simple names are unambiguous everywhere.
    /// </summary>
    public static Compilation Create(string daprClientMethods, string? extraTypes = null)
    {
        // Extra types are placed INSIDE the Dapr namespace so that
        // GetTypeByMetadataName("Dapr.Client.Autogen.Grpc.v1.Foo") resolves them.
        // Method stubs use global:: to bypass the Grpc sub-namespace collision.
        var outerClass = $$"""
            namespace Dapr.Client.Autogen.Grpc.v1
            {
                {{extraTypes ?? string.Empty}}

                public static class Dapr
                {
                    public class DaprClient
                    {
                        {{daprClientMethods}}
                    }
                }
            }
            """;

        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(GrpcCoreStubs),
            CSharpSyntaxTree.ParseText(outerClass),
        };

        return CSharpCompilation.Create(
            assemblyName: "StubAssembly",
            syntaxTrees: trees,
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    // ------------------------------------------------------------------
    // Pre-built stub sets for common test scenarios
    // NOTE: Method stubs MUST use global:: to avoid the namespace resolution
    //       issue described in the class remarks above.
    // ------------------------------------------------------------------

    /// <summary>
    /// Stable + Alpha1 variants that share identical request/response types.
    /// Expected classification: AutoCompatible.
    /// </summary>
    public static Compilation WithIdenticalTypeVariants() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<FooResponse> FooAsync(FooRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<FooResponse> FooAlpha1Async(FooRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class FooRequest  { }
            public class FooResponse { }
            """);

    /// <summary>
    /// Stable + Alpha1 variants with structurally compatible (but differently-named) types.
    /// Both request types have a writable <c>Name</c> string property.
    /// Expected classification: AutoCompatible.
    /// </summary>
    public static Compilation WithCompatibleDifferentTypes() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<BarResponse> BarAsync(BarRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<BarResponseAlpha1> BarAlpha1Async(BarRequestAlpha1 r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class BarRequest       { public string Name { get; set; } }
            public class BarResponse      { public string Result { get; set; } }
            public class BarRequestAlpha1  { public string Name { get; set; } }
            public class BarResponseAlpha1 { public string Result { get; set; } }
            """);

    /// <summary>
    /// Two Alpha variants whose request/response types are schema-incompatible.
    /// Expected classification: SchemaDivergent.
    /// </summary>
    public static Compilation WithIncompatibleAlphaVariants() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<BazResponseAlpha2> BazAlpha2Async(BazRequestAlpha2 r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<BazResponseAlpha1> BazAlpha1Async(BazRequestAlpha1 r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class BazRequestAlpha1  { public string SimpleField { get; set; } }
            public class BazResponseAlpha1 { public string SimpleResult { get; set; } }
            public class BazRequestAlpha2  { public int TotallyDifferentField { get; set; } }
            public class BazResponseAlpha2 { public int TotallyDifferentResult { get; set; } }
            """);

    /// <summary>
    /// Stable variant + [Obsolete]-tagged Alpha1 variant with identical types — mirrors the
    /// real Dapr gRPC stubs where alpha methods are marked deprecated once the stable API lands.
    /// Expected classification: AutoCompatible (obsolete is included as a fallback).
    /// </summary>
    public static Compilation WithObsoleteAlphaVariant() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<CorfResponse> CorfAsync(CorfRequest r, global::Grpc.Core.CallOptions o) { return null; }
            [global::System.ObsoleteAttribute]
            public global::Grpc.Core.AsyncUnaryCall<CorfResponse> CorfAlpha1Async(CorfRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class CorfRequest  { }
            public class CorfResponse { }
            """);

    /// <summary>Single stable variant with no prior Alpha. Expected: PassThrough.</summary>
    public static Compilation WithSingleStableVariant() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<QuxResponse> QuxAsync(QuxRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class QuxRequest  { }
            public class QuxResponse { }
            """);

    // ------------------------------------------------------------------
    // Stubs for EmitRequestConversion / EmitResponseConversion inner paths
    // ------------------------------------------------------------------

    /// <summary>
    /// Stable + Alpha1 where both share the SAME request type but use DIFFERENT response types.
    /// This forces the else-block in EmitAutoCompatibleMethod while making the EmitRequestConversion
    /// inner same-type check emit <c>var __fallbackRequest = request;</c> rather than a new object,
    /// and triggers the full EmitResponseConversion (new object + property copy).
    /// Expected classification: AutoCompatible.
    /// </summary>
    public static Compilation WithSameRequestDifferentResponse() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<XyzResponse> XyzAsync(XyzRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<XyzResponseAlpha1> XyzAlpha1Async(XyzRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class XyzRequest        { public string Name { get; set; } }
            public class XyzResponse       { public string Result { get; set; } }
            public class XyzResponseAlpha1 { public string Result { get; set; } }
            """);

    /// <summary>
    /// Stable + Alpha1 where variants use DIFFERENT request types but share the SAME response type.
    /// This forces the else-block in EmitAutoCompatibleMethod while triggering the full
    /// EmitRequestConversion (new object + property copy) and making the EmitResponseConversion
    /// inner same-type check emit <c>return __fallbackResponse;</c>.
    /// Expected classification: AutoCompatible.
    /// </summary>
    public static Compilation WithDifferentRequestSameResponse() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<AbcResponse> AbcAsync(AbcRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<AbcResponse> AbcAlpha1Async(AbcRequestAlpha1 r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class AbcRequest       { public string Name { get; set; } }
            public class AbcRequestAlpha1 { public string Name { get; set; } }
            public class AbcResponse      { public string Result { get; set; } }
            """);

    // ------------------------------------------------------------------
    // Stubs for TypeNamesCompatible recursive generic-arg check
    // ------------------------------------------------------------------

    /// <summary>
    /// Two Alpha variants where both request types have the same field name ("Entries") and the
    /// same outer collection type ("RepeatedField") but different generic type arguments.
    /// TypeNamesCompatible must recurse into the type arguments and return false, causing
    /// AreFieldsCompatible → false → SchemaDivergent.
    /// </summary>
    public static Compilation WithIncompatibleGenericArgTypes() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<TroxResponseAlpha2> TroxAlpha2Async(TroxRequestAlpha2 r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<TroxResponseAlpha1> TroxAlpha1Async(TroxRequestAlpha1 r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class RepeatedField<T> { }
            public class ItemA { }
            public class ItemB { }
            public class TroxRequestAlpha2  { public RepeatedField<ItemA> Entries { get; } }
            public class TroxResponseAlpha2 { }
            public class TroxRequestAlpha1  { public RepeatedField<ItemB> Entries { get; } }
            public class TroxResponseAlpha1 { }
            """);

    /// <summary>
    /// Stable + Beta1 variants with identical types.
    /// Expected classification: AutoCompatible; fallback at Beta level.
    /// </summary>
    public static Compilation WithBetaVariants() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<GarplyResponse> GarplyAsync(GarplyRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<GarplyResponse> GarplyBeta1Async(GarplyRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class GarplyRequest  { }
            public class GarplyResponse { }
            """);

    /// <summary>
    /// Stable + RC1 variants with identical types.
    /// Expected classification: AutoCompatible; fallback at ReleaseCandidate level.
    /// </summary>
    public static Compilation WithRCVariants() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<WaldoResponse> WaldoAsync(WaldoRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<WaldoResponse> WaldoRC1Async(WaldoRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class WaldoRequest  { }
            public class WaldoResponse { }
            """);

    /// <summary>
    /// Stable + Alpha2 + Alpha1 variants with identical types.
    /// Expected classification: AutoCompatible; two fallbacks ordered Alpha2 then Alpha1.
    /// </summary>
    public static Compilation WithMultipleFallbacks() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<GraultResponse> GraultAsync(GraultRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<GraultResponse> GraultAlpha2Async(GraultRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<GraultResponse> GraultAlpha1Async(GraultRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class GraultRequest  { }
            public class GraultResponse { }
            """);

    /// <summary>
    /// Two Alpha variants where both variants have the same field name but different field types
    /// (string vs int). Expected classification: SchemaDivergent.
    /// Tests TypeNamesCompatible returning false.
    /// </summary>
    public static Compilation WithIncompatibleFieldTypes() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<PlughResponseAlpha2> PlughAlpha2Async(PlughRequestAlpha2 r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<PlughResponseAlpha1> PlughAlpha1Async(PlughRequestAlpha1 r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class PlughRequestAlpha1  { public string Value { get; set; } }
            public class PlughResponseAlpha1 { }
            public class PlughRequestAlpha2  { public int Value { get; set; } }
            public class PlughResponseAlpha2 { }
            """);

    /// <summary>
    /// DaprClient containing only methods that do NOT satisfy IsAsyncUnaryWithCallOptions:
    /// no "Async" suffix, wrong parameter count, wrong return type.
    /// Expected: AnalyzeCompilation returns null (no valid variants found).
    /// </summary>
    public static Compilation WithOnlyNonMatchingMethods() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<NopResponse> NopNoSuffix(NopRequest r, global::Grpc.Core.CallOptions o) { return null; }
            public global::Grpc.Core.AsyncUnaryCall<NopResponse> NopOneParamAsync(NopRequest r) { return null; }
            public global::System.Threading.Tasks.Task<NopResponse> NopWrongReturnAsync(NopRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class NopRequest  { }
            public class NopResponse { }
            """);

    /// <summary>
    /// Stable + Alpha1 variants where request/response types differ but are compatible,
    /// and the older type has RepeatedField, MapField, read-only scalar, and writable scalar fields.
    /// Used to exercise all branches of EmitPropertyCopy.
    /// </summary>
    public static Compilation WithCollectionFields()
    {
        // Stub RepeatedField<T> and MapField<K,V> in a separate namespace
        const string collectionStubs = """
            namespace Proto.Collections
            {
                public class RepeatedField<T>
                {
                    public void AddRange(global::System.Collections.Generic.IEnumerable<T> values) { }
                }
                public class MapField<TKey, TValue>
                {
                    public TValue this[TKey key] { get { return default!; } set { } }
                }
            }
            """;

        const string daprClientSource = """
            namespace Dapr.Client.Autogen.Grpc.v1
            {
                public class CorgeRequest
                {
                    public string Name { get; set; } = string.Empty;
                    public global::Proto.Collections.RepeatedField<string> Tags { get; } = new();
                    public global::Proto.Collections.MapField<string, int> Labels { get; } = new();
                    public int ReadOnlyCount { get; }
                    public string Extra { get; set; } = string.Empty;
                }
                public class CorgeResponse { public string Result { get; set; } = string.Empty; }

                public class CorgeRequestAlpha1
                {
                    public string Name { get; set; } = string.Empty;
                    public global::Proto.Collections.RepeatedField<string> Tags { get; } = new();
                    public global::Proto.Collections.MapField<string, int> Labels { get; } = new();
                    public int ReadOnlyCount { get; }
                }
                public class CorgeResponseAlpha1 { public string Result { get; set; } = string.Empty; }

                public static class Dapr
                {
                    public class DaprClient
                    {
                        public global::Grpc.Core.AsyncUnaryCall<CorgeResponse> CorgeAsync(
                            CorgeRequest r, global::Grpc.Core.CallOptions o) { return null!; }
                        public global::Grpc.Core.AsyncUnaryCall<CorgeResponseAlpha1> CorgeAlpha1Async(
                            CorgeRequestAlpha1 r, global::Grpc.Core.CallOptions o) { return null!; }
                    }
                }
            }
            """;

        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(GrpcCoreStubs),
            CSharpSyntaxTree.ParseText(collectionStubs),
            CSharpSyntaxTree.ParseText(daprClientSource),
        };

        return CSharpCompilation.Create(
            assemblyName: "StubAssembly",
            syntaxTrees: trees,
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
