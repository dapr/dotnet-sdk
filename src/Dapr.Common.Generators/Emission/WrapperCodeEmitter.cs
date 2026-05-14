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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dapr.Common.Generators.Analysis;
using Dapr.Common.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Dapr.Common.Generators.Emission;

/// <summary>
/// Emits the C# source code for <c>IVersionAwareDaprClient</c> and
/// <c>VersionAwareDaprClient</c> from the analysed <see cref="MethodGroup"/> list.
/// </summary>
internal static class WrapperCodeEmitter
{
    internal const string InterfaceName = "IVersionAwareDaprClient";
    internal const string ClassName = "VersionAwareDaprClient";
    private const string TargetNamespace = "Dapr.Common";

    private const string InnerFieldName = "_inner";
    private const string CapabilitiesFieldName = "_capabilities";

    /// <summary>
    /// Emits the interface and partial class source files.
    /// </summary>
    /// <returns>
    /// A tuple of (interfaceSource, classSource) ready for <c>SourceProductionContext.AddSource</c>.
    /// </returns>
    public static (string interfaceSource, string classSource) Emit(IReadOnlyList<MethodGroup> groups)
        => (EmitInterface(groups), EmitClass(groups));

    // -------------------------------------------------------------------------
    // Interface
    // -------------------------------------------------------------------------

    internal static string EmitInterface(IReadOnlyList<MethodGroup> groups)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine($"namespace {TargetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Exposes the most-recent Dapr gRPC API variants with automatic runtime-version");
        sb.AppendLine("/// fallback. Implemented by the generated <see cref=\"VersionAwareDaprClient\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal interface {InterfaceName}");
        sb.AppendLine("{");

        foreach (var group in groups)
        {
            var (reqFqn, respFqn) = GetRequestResponseFqns(group.MostRecent);
            sb.Append($"    global::System.Threading.Tasks.Task<{respFqn}> {group.BaseName}Async(");
            sb.Append($"{reqFqn} request, ");
            sb.AppendLine("global::Grpc.Core.CallOptions options);");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Implementation class
    // -------------------------------------------------------------------------

    internal static string EmitClass(IReadOnlyList<MethodGroup> groups)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine($"namespace {TargetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Version-aware gRPC wrapper that automatically selects the highest-supported");
        sb.AppendLine("/// Dapr runtime API variant for each operation and falls back to older variants");
        sb.AppendLine("/// when the connected runtime does not yet support the newest API.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal sealed partial class {ClassName} : {InterfaceName}");
        sb.AppendLine("{");

        // Fields
        sb.AppendLine($"    private readonly global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient {InnerFieldName};");
        sb.AppendLine($"    private readonly global::Dapr.Common.IDaprRuntimeCapabilities {CapabilitiesFieldName};");
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    public {ClassName}(");
        sb.AppendLine("        global::Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient inner,");
        sb.AppendLine("        global::Dapr.Common.IDaprRuntimeCapabilities capabilities)");
        sb.AppendLine("    {");
        sb.AppendLine($"        {InnerFieldName} = inner;");
        sb.AppendLine($"        {CapabilitiesFieldName} = capabilities;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Methods
        foreach (var group in groups)
        {
            EmitMethod(sb, group);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Method emission
    // -------------------------------------------------------------------------

    private static void EmitMethod(StringBuilder sb, MethodGroup group)
    {
        switch (group.Classification)
        {
            case MethodClassification.PassThrough:
                EmitPassThroughMethod(sb, group);
                break;
            case MethodClassification.AutoCompatible:
                EmitAutoCompatibleMethod(sb, group);
                break;
            case MethodClassification.SchemaDivergent:
                EmitSchemaDivergentMethod(sb, group);
                break;
        }
    }

    /// <summary>
    /// PassThrough: single variant, direct delegation via expression body.
    /// </summary>
    private static void EmitPassThroughMethod(StringBuilder sb, MethodGroup group)
    {
        var (reqFqn, respFqn) = GetRequestResponseFqns(group.MostRecent);
        var m = group.MostRecent;

        sb.AppendLine($"    /// <inheritdoc/>");
        sb.Append($"    public global::System.Threading.Tasks.Task<{respFqn}> {group.BaseName}Async(");
        sb.AppendLine($"{reqFqn} request, global::Grpc.Core.CallOptions options)");
        sb.AppendLine($"        => {InnerFieldName}.{m.CSharpMethodName}(request, options).ResponseAsync;");
        sb.AppendLine();
    }

    /// <summary>
    /// AutoCompatible: async capability-check chain with field-mapping for fallback variants.
    /// </summary>
    private static void EmitAutoCompatibleMethod(StringBuilder sb, MethodGroup group)
    {
        var mostRecent = group.MostRecent;
        var (reqFqn, respFqn) = GetRequestResponseFqns(mostRecent);

        sb.AppendLine($"    /// <inheritdoc/>");
        sb.Append($"    public async global::System.Threading.Tasks.Task<{respFqn}> {group.BaseName}Async(");
        sb.AppendLine($"{reqFqn} request, global::Grpc.Core.CallOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        var __ct = options.CancellationToken;");
        sb.AppendLine();

        // Most-recent variant: check capability then call, catching Unimplemented so that a method
        // defined in the runtime's proto but not yet backed by a handler falls through to the older variant.
        // Also catch Unknown with the Dapr proxy-routing error that older runtimes emit when they cannot
        // match the method internally and attempt (and fail) to forward it as a service invocation.
        sb.AppendLine($"        if (await {CapabilitiesFieldName}.SupportsMethodAsync(\"{mostRecent.FullyQualifiedMethodName}\", __ct).ConfigureAwait(false))");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                return await {InnerFieldName}.{mostRecent.CSharpMethodName}(request, options).ResponseAsync.ConfigureAwait(false);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (global::Grpc.Core.RpcException __implEx) when (");
        sb.AppendLine("                __implEx.StatusCode == global::Grpc.Core.StatusCode.Unimplemented ||");
        sb.AppendLine("                (__implEx.StatusCode == global::Grpc.Core.StatusCode.Unknown &&");
        sb.AppendLine("                 __implEx.Status.Detail.Contains(\"dapr-callee-app-id or dapr-app-id not found\")))");
        sb.AppendLine("            {");
        sb.AppendLine("                // Method is in the runtime proto but not yet implemented, or the runtime");
        sb.AppendLine("                // does not recognise the method and attempted to proxy it as a service invocation.");
        sb.AppendLine("                // Either way, fall through to the older variant.");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Fallback variants
        foreach (var fallback in group.Fallbacks)
        {
            var sameRequest = SymbolEqualityComparer.Default.Equals(mostRecent.RequestType, fallback.RequestType);
            var sameResponse = SymbolEqualityComparer.Default.Equals(mostRecent.ResponseType, fallback.ResponseType);

            sb.AppendLine($"        if (await {CapabilitiesFieldName}.SupportsMethodAsync(\"{fallback.FullyQualifiedMethodName}\", __ct).ConfigureAwait(false))");

            if (sameRequest && sameResponse)
            {
                // No type conversion needed — compact one-liner
                sb.AppendLine($"            return await {InnerFieldName}.{fallback.CSharpMethodName}(request, options).ResponseAsync.ConfigureAwait(false);");
            }
            else
            {
                sb.AppendLine("        {");
                EmitRequestConversion(sb, mostRecent, fallback, "            ");
                sb.AppendLine($"            var __fallbackResponse = await {InnerFieldName}.{fallback.CSharpMethodName}(__fallbackRequest, options).ResponseAsync.ConfigureAwait(false);");
                EmitResponseConversion(sb, mostRecent, fallback, "            ");
                sb.AppendLine("        }");
            }

            sb.AppendLine();
        }

        // Nothing supported → exception
        EmitFeatureNotAvailableThrow(sb, group, "        ");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// SchemaDivergent: async capability-check for most-recent; older fallbacks throw
    /// <see cref="System.NotSupportedException"/> because the schemas are incompatible.
    /// The class is <c>partial</c> so SDK maintainers can override individual methods.
    /// </summary>
    private static void EmitSchemaDivergentMethod(StringBuilder sb, MethodGroup group)
    {
        var mostRecent = group.MostRecent;
        var (reqFqn, respFqn) = GetRequestResponseFqns(mostRecent);

        sb.AppendLine($"    /// <inheritdoc/>");
        sb.Append($"    public async global::System.Threading.Tasks.Task<{respFqn}> {group.BaseName}Async(");
        sb.AppendLine($"{reqFqn} request, global::Grpc.Core.CallOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        var __ct = options.CancellationToken;");
        sb.AppendLine();

        // Most-recent variant: catch Unimplemented so a proto-defined-but-not-yet-handled method
        // falls through to the schema-divergent NotSupportedException path rather than surfacing a raw RpcException.
        // Also catch Unknown with the Dapr proxy-routing error that older runtimes emit when they cannot
        // match the method internally and attempt (and fail) to forward it as a service invocation.
        sb.AppendLine($"        if (await {CapabilitiesFieldName}.SupportsMethodAsync(\"{mostRecent.FullyQualifiedMethodName}\", __ct).ConfigureAwait(false))");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                return await {InnerFieldName}.{mostRecent.CSharpMethodName}(request, options).ResponseAsync.ConfigureAwait(false);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (global::Grpc.Core.RpcException __implEx) when (");
        sb.AppendLine("                __implEx.StatusCode == global::Grpc.Core.StatusCode.Unimplemented ||");
        sb.AppendLine("                (__implEx.StatusCode == global::Grpc.Core.StatusCode.Unknown &&");
        sb.AppendLine("                 __implEx.Status.Detail.Contains(\"dapr-callee-app-id or dapr-app-id not found\")))");
        sb.AppendLine("            {");
        sb.AppendLine("                // Method is in the runtime proto but not yet implemented, or the runtime");
        sb.AppendLine("                // does not recognise the method and attempted to proxy it as a service invocation.");
        sb.AppendLine("                // Either way, fall through to the older variant.");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Older, incompatible variants → NotSupportedException
        foreach (var fallback in group.Fallbacks)
        {
            sb.AppendLine($"        if (await {CapabilitiesFieldName}.SupportsMethodAsync(\"{fallback.FullyQualifiedMethodName}\", __ct).ConfigureAwait(false))");
            sb.AppendLine("            throw new global::System.NotSupportedException(");
            sb.AppendLine($"                \"The '{group.BaseName}' operation cannot automatically fall back from '{mostRecent.GrpcMethodName}' to '{fallback.GrpcMethodName}' \" +");
            sb.AppendLine($"                \"because the schemas are incompatible. Provide a partial-class override of {ClassName} to handle this older runtime version.\");");
            sb.AppendLine();
        }

        EmitFeatureNotAvailableThrow(sb, group, "        ");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // -------------------------------------------------------------------------
    // Type-mapping helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits code that converts <paramref name="mostRecent"/>'s request type to
    /// <paramref name="fallback"/>'s request type, assigning the result to a local
    /// variable named <c>__fallbackRequest</c>.
    /// </summary>
    private static void EmitRequestConversion(
        StringBuilder sb,
        MethodVariant mostRecent,
        MethodVariant fallback,
        string indent)
    {
        if (SymbolEqualityComparer.Default.Equals(mostRecent.RequestType, fallback.RequestType))
        {
            sb.AppendLine($"{indent}var __fallbackRequest = request;");
            return;
        }

        var fallbackReqFqn = fallback.RequestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        sb.AppendLine($"{indent}var __fallbackRequest = new {fallbackReqFqn}();");

        // Copy every compatible property from the new request into the old one.
        // We iterate the *fallback* type's properties so we only touch fields that exist there.
        foreach (var prop in DaprClientAnalyzer.GetUserInstanceProperties(fallback.RequestType))
        {
            EmitPropertyCopy(sb, prop, "request", "__fallbackRequest", indent);
        }
    }

    /// <summary>
    /// Emits code that converts <paramref name="fallback"/>'s response into
    /// <paramref name="mostRecent"/>'s response type, ending with a <c>return</c>
    /// statement.
    /// </summary>
    private static void EmitResponseConversion(
        StringBuilder sb,
        MethodVariant mostRecent,
        MethodVariant fallback,
        string indent)
    {
        if (SymbolEqualityComparer.Default.Equals(mostRecent.ResponseType, fallback.ResponseType))
        {
            sb.AppendLine($"{indent}return __fallbackResponse;");
            return;
        }

        var newerRespFqn = mostRecent.ResponseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        sb.AppendLine($"{indent}var __convertedResponse = new {newerRespFqn}();");

        // Copy every compatible property from the old response into the new one.
        // We iterate the *fallback* type's properties so we only copy fields that exist there;
        // the newer type may have additional fields which remain at their defaults.
        foreach (var prop in DaprClientAnalyzer.GetUserInstanceProperties(fallback.ResponseType))
        {
            EmitPropertyCopy(sb, prop, "__fallbackResponse", "__convertedResponse", indent);
        }

        sb.AppendLine($"{indent}return __convertedResponse;");
    }

    /// <summary>
    /// Emits a single property copy statement from <paramref name="source"/> to
    /// <paramref name="target"/>, choosing the correct idiom for repeated / map / scalar fields.
    /// Read-only non-collection properties (e.g. proto <c>HasXxx</c> sentinels) are skipped.
    /// </summary>
    private static void EmitPropertyCopy(
        StringBuilder sb,
        IPropertySymbol prop,
        string source,
        string target,
        string indent)
    {
        var typeName = prop.Type.Name;

        if (typeName == "RepeatedField")
        {
            // RepeatedField<T>.AddRange() is the idiomatic copy
            sb.AppendLine($"{indent}{target}.{prop.Name}.AddRange({source}.{prop.Name});");
        }
        else if (typeName == "MapField")
        {
            // MapField<K,V>: iterate and add each entry
            sb.AppendLine($"{indent}foreach (var __kvp in {source}.{prop.Name})");
            sb.AppendLine($"{indent}    {target}.{prop.Name}[__kvp.Key] = __kvp.Value;");
        }
        else if (!prop.IsReadOnly)
        {
            // Writable scalar / message field
            sb.AppendLine($"{indent}{target}.{prop.Name} = {source}.{prop.Name};");
        }
        // else: read-only non-collection (e.g. proto HasXxx bool) — skip
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static void EmitFeatureNotAvailableThrow(StringBuilder sb, MethodGroup group, string indent)
    {
        var allVariants = new[] { group.MostRecent }.Concat(group.Fallbacks);
        var variantList = string.Join(", ", allVariants.Select(v => $"\"{v.FullyQualifiedMethodName}\""));
        sb.AppendLine($"{indent}throw new global::Dapr.Common.DaprFeatureNotAvailableException(\"{group.BaseName}\", new string[] {{ {variantList} }});");
    }

    private static (string reqFqn, string respFqn) GetRequestResponseFqns(MethodVariant variant) =>
        (
            variant.RequestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            variant.ResponseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );

    private static void AppendFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// The generated fallback chain intentionally calls [Obsolete]-tagged alpha/beta gRPC");
        sb.AppendLine("// stub methods so that older Dapr runtimes are still supported.");
        sb.AppendLine("// CS0612: [Obsolete] with no message. CS0618: [Obsolete(\"message\")].");
        sb.AppendLine("#pragma warning disable CS0612, CS0618");
        sb.AppendLine();
    }
}
