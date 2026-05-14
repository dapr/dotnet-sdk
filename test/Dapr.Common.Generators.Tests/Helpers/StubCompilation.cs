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

    /// <summary>Single stable variant with no prior Alpha. Expected: PassThrough.</summary>
    public static Compilation WithSingleStableVariant() => Create(
        daprClientMethods: """
            public global::Grpc.Core.AsyncUnaryCall<QuxResponse> QuxAsync(QuxRequest r, global::Grpc.Core.CallOptions o) { return null; }
            """,
        extraTypes: """
            public class QuxRequest  { }
            public class QuxResponse { }
            """);
}
