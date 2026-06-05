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
using Dapr.Common.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Dapr.Common.Generators.Analysis;

/// <summary>
/// Discovers gRPC method variants on the Dapr runtime client and classifies them
/// into <see cref="MethodGroup"/> instances for code generation.
/// </summary>
internal static class DaprClientAnalyzer
{
    private const string DaprServiceNamespace = "dapr.proto.runtime.v1.Dapr";

    // The Dapr gRPC client is a nested class: Dapr.Client.Autogen.Grpc.v1.Dapr (static class) + DaprClient (nested).
    private const string DaprOuterClassName = "Dapr.Client.Autogen.Grpc.v1.Dapr";
    private const string DaprClientNestedName = "DaprClient";
    private const string CallOptionsMetadataName = "Grpc.Core.CallOptions";
    private const string AsyncUnaryCallMetadataName = "Grpc.Core.AsyncUnaryCall`1";

    /// <summary>
    /// Finds the <c>Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient</c> type in the compilation,
    /// enumerates its unary async methods, groups them by base name, and classifies each group.
    /// Returns null if the DaprClient type or required Grpc.Core types are not found.
    /// </summary>
    public static IReadOnlyList<MethodGroup>? AnalyzeCompilation(Compilation compilation)
    {
        // Locate DaprClient (nested type: Dapr.Client.Autogen.Grpc.v1.Dapr+DaprClient)
        var daprClientType = FindDaprClientType(compilation);
        if (daprClientType is null)
            return null;

        var callOptionsType = compilation.GetTypeByMetadataName(CallOptionsMetadataName);
        if (callOptionsType is null)
            return null;

        var asyncUnaryCallType = compilation.GetTypeByMetadataName(AsyncUnaryCallMetadataName);
        if (asyncUnaryCallType is null)
            return null;

        // Enumerate async unary methods with signature: (TRequest, CallOptions)
        var variants = daprClientType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => IsAsyncUnaryWithCallOptions(m, callOptionsType, asyncUnaryCallType))
            .Select(ParseVariant)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();

        if (variants.Count == 0)
            return null;

        // Group by base name and sort by descending maturity
        var grouped = variants
            .GroupBy(v => v.BaseName)
            .Select(g => BuildGroup(g.Key, g.ToList()))
            .ToList();

        return grouped;
    }

    private static INamedTypeSymbol? FindDaprClientType(Compilation compilation)
    {
        // Try metadata name with nested-type separator ("+")
        var type = compilation.GetTypeByMetadataName($"{DaprOuterClassName}+{DaprClientNestedName}");
        if (type is not null)
            return type;

        // Fallback: walk namespace tree manually
        var parts = DaprOuterClassName.Split('.');
        INamespaceOrTypeSymbol current = compilation.GlobalNamespace;
        foreach (var part in parts)
        {
            var next = current.GetMembers(part).OfType<INamespaceOrTypeSymbol>().FirstOrDefault();
            if (next is null)
                return null;
            current = next;
        }
        return (current as INamedTypeSymbol)?.GetTypeMembers(DaprClientNestedName).FirstOrDefault();
    }

    private static bool IsAsyncUnaryWithCallOptions(
        IMethodSymbol method,
        INamedTypeSymbol callOptionsType,
        INamedTypeSymbol asyncUnaryCallType)
    {
        if (!method.Name.EndsWith("Async"))
            return false;

        // Note: obsolete methods ARE included. The [Obsolete] attribute on gRPC stubs
        // marks the older alpha/beta variants that have been promoted to stable — those
        // are precisely the fallback targets we need for older Dapr runtimes.

        if (method.Parameters.Length != 2)
            return false;

        // Second parameter must be CallOptions
        if (!SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, callOptionsType))
            return false;

        // Return type must be AsyncUnaryCall<T>
        if (method.ReturnType is not INamedTypeSymbol returnNamed)
            return false;

        return SymbolEqualityComparer.Default.Equals(
            returnNamed.OriginalDefinition,
            asyncUnaryCallType);
    }

    private static MethodVariant? ParseVariant(IMethodSymbol method)
    {
        // Strip "Async" suffix to get the gRPC method name
        var grpcName = method.Name.Substring(0, method.Name.Length - "Async".Length);

        // Parse base name and maturity suffix
        var (baseName, suffix) = SplitMaturitySuffix(grpcName);
        var (level, levelNumber) = ParseMaturityLevel(suffix);

        // Extract request type from first parameter
        if (method.Parameters[0].Type is not INamedTypeSymbol requestType)
            return null;

        // Extract response type from AsyncUnaryCall<TResponse>
        if (method.ReturnType is not INamedTypeSymbol returnNamed)
            return null;
        if (returnNamed.TypeArguments.Length != 1)
            return null;
        if (returnNamed.TypeArguments[0] is not INamedTypeSymbol responseType)
            return null;

        return new MethodVariant
        {
            CSharpMethodName = method.Name,
            BaseName = baseName,
            Suffix = suffix,
            Level = level,
            LevelNumber = levelNumber,
            GrpcMethodName = grpcName,
            FullyQualifiedMethodName = $"{DaprServiceNamespace}/{grpcName}",
            Symbol = method,
            RequestType = requestType,
            ResponseType = responseType
        };
    }

    /// <summary>
    /// Splits a gRPC method name (without "Async") into base name and maturity suffix.
    /// E.g. "ScheduleJobAlpha1" → ("ScheduleJob", "Alpha1").
    ///      "ListJobs"          → ("ListJobs", "").
    ///      "ConverseAlpha2"    → ("Converse", "Alpha2").
    /// </summary>
    internal static (string baseName, string suffix) SplitMaturitySuffix(string grpcName)
    {
        // Try suffixes in descending specificity: RC, Beta, Alpha
        string[] prefixes = ["RC", "Beta", "Alpha"];
        foreach (var prefix in prefixes)
        {
            var idx = grpcName.LastIndexOf(prefix, System.StringComparison.Ordinal);
            if (idx <= 0)
                continue;

            var after = grpcName.Substring(idx + prefix.Length);
            if (after.Length == 0 || IsAllDigits(after))
                return (grpcName.Substring(0, idx), grpcName.Substring(idx));
        }

        return (grpcName, string.Empty);
    }

    private static (MaturityLevel level, int number) ParseMaturityLevel(string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
            return (MaturityLevel.Stable, 0);

        if (suffix.StartsWith("RC"))
        {
            int.TryParse(suffix.Substring(2), out var n);
            return (MaturityLevel.ReleaseCandidate, n);
        }

        if (suffix.StartsWith("Beta"))
        {
            int.TryParse(suffix.Substring(4), out var n);
            return (MaturityLevel.Beta, n);
        }

        if (suffix.StartsWith("Alpha"))
        {
            int.TryParse(suffix.Substring(5), out var n);
            return (MaturityLevel.Alpha, n);
        }

        return (MaturityLevel.Stable, 0);
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
            if (c is < '0' or > '9')
                return false;
        return true;
    }

    private static MethodGroup BuildGroup(string baseName, List<MethodVariant> variants)
    {
        // Sort: Stable first, then by level descending, then by level number descending
        variants.Sort((a, b) =>
        {
            var levelCmp = b.Level.CompareTo(a.Level);
            return levelCmp != 0 ? levelCmp : b.LevelNumber.CompareTo(a.LevelNumber);
        });

        var mostRecent = variants[0];
        var fallbacks = variants.Skip(1).ToList();

        var classification = fallbacks.Count == 0
            ? MethodClassification.PassThrough
            : DetermineCompatibility(mostRecent, fallbacks);

        return new MethodGroup
        {
            BaseName = baseName,
            MostRecent = mostRecent,
            Fallbacks = fallbacks,
            Classification = classification
        };
    }

    /// <summary>
    /// Returns AutoCompatible if every fallback variant's request AND response types can be
    /// automatically field-mapped from the most-recent variant's types (either same type,
    /// or all instance user-fields of the older type have a name-matching field in the newer type).
    /// Returns SchemaDivergent if any mapping would be lossy.
    /// </summary>
    private static MethodClassification DetermineCompatibility(
        MethodVariant mostRecent,
        IReadOnlyList<MethodVariant> fallbacks) =>
        fallbacks.Any(fallback => !AreFieldsCompatible(mostRecent.RequestType, fallback.RequestType) ||
                                  !AreFieldsCompatible(mostRecent.ResponseType, fallback.ResponseType))
            ? MethodClassification.SchemaDivergent
            : MethodClassification.AutoCompatible;

    /// <summary>
    /// Returns true if all user-facing instance properties of <paramref name="olderType"/>
    /// have a name-and-type-compatible counterpart in <paramref name="newerType"/>.
    /// Identical types are trivially compatible.
    /// </summary>
    internal static bool AreFieldsCompatible(INamedTypeSymbol newerType, INamedTypeSymbol olderType)
    {
        if (SymbolEqualityComparer.Default.Equals(newerType, olderType))
            return true;

        var olderProps = GetUserInstanceProperties(olderType);
        if (olderProps.Count == 0)
            return true; // Empty old type maps trivially to any newer type

        var newerByName = GetUserInstanceProperties(newerType)
            .ToDictionary(p => NormalizePropertyName(p.Name), p => p);

        foreach (var oldProp in olderProps)
        {
            if (!newerByName.TryGetValue(NormalizePropertyName(oldProp.Name), out var newProp))
                return false;

            // Check that the type names match recursively (including generic type arguments).
            // This prevents RepeatedField<ConversationInput> and RepeatedField<ConversationInputAlpha2>
            // from being treated as compatible just because they share the container type name.
            if (!TypeNamesCompatible(newProp.Type, oldProp.Type))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if <paramref name="newerType"/> and <paramref name="olderType"/> have the
    /// same simple name and all their generic type arguments recursively match by name.
    /// This is an intentionally loose check — it compares names, not symbols — so that
    /// structurally identical types from different assemblies are still treated as compatible.
    /// </summary>
    private static bool TypeNamesCompatible(ITypeSymbol newerType, ITypeSymbol olderType)
    {
        if (newerType.Name != olderType.Name)
            return false;

        if (newerType is not INamedTypeSymbol newerNamed || olderType is not INamedTypeSymbol olderNamed)
            return true;

        if (newerNamed.TypeArguments.Length != olderNamed.TypeArguments.Length)
            return false;

        return !newerNamed.TypeArguments.Where((t, i) => !TypeNamesCompatible(t, olderNamed.TypeArguments[i])).Any();

    }

    /// <summary>
    /// Returns all public, non-static properties of a proto-generated type that
    /// represent user-defined fields (excludes the static infrastructure "Parser"
    /// and "Descriptor" properties).
    /// </summary>
    internal static IReadOnlyList<IPropertySymbol> GetUserInstanceProperties(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic)
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .Where(p => p.Name != "Parser" && p.Name != "Descriptor")
            .ToList();

    /// <summary>
    /// Normalizes a property name for case/underscore-insensitive matching.
    /// "NamePrefix" and "name_prefix" both normalize to "nameprefix".
    /// </summary>
    private static string NormalizePropertyName(string name)
        => name.Replace("_", "").ToLowerInvariant();
}
