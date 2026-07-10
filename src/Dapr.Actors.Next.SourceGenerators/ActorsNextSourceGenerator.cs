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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dapr.Actors.Next.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.Actors.Next.SourceGenerators;

/// <summary>
/// Generates reflection-free actor proxies, dispatchers, factories, registration, registry manifest, and JSON context hints.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ActorsNextSourceGenerator : IIncrementalGenerator
{
    private const string IActorMetadataName = "Dapr.Actors.Next.Abstractions.IActor";
    private const string DaprActorAttributeMetadataName = "Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute";
    private const string GenerateActorClientAttributeMetadataName = "Dapr.Actors.Next.Abstractions.Attributes.GenerateActorClientAttribute";
    private const string UpcasterMetadataName = "Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster`2";
    private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";
    private const string ScanReferencesPropertyName = "build_property.DaprActorsScanReferences";
    private static readonly DiagnosticDescriptor NonUniqueFoldPathDiagnostic = new(
        "DAPR1421",
        "Actor state migration family has more than one fold path",
        "Actor state migration family '{0}' has more than one fold path; generated migration metadata was skipped for this family",
        "Dapr.Actors.Next.SourceGenerators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonAppendOnlyChainDiagnostic = new(
        "DAPR1422",
        "Actor state migration family is not append-only",
        "Actor state migration family '{0}' is not an append-only chain; generated migration metadata was skipped for this family",
        "Dapr.Actors.Next.SourceGenerators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue(ScanReferencesPropertyName, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        var knownSymbols = context.CompilationProvider.Select((compilation, _) => new KnownSymbols(
            compilation.GetTypeByMetadataName(IActorMetadataName),
            compilation.GetTypeByMetadataName(DaprActorAttributeMetadataName),
            compilation.GetTypeByMetadataName(GenerateActorClientAttributeMetadataName),
            compilation.GetTypeByMetadataName(UpcasterMetadataName),
            compilation.GetTypeByMetadataName(CancellationTokenMetadataName)));

        var interfaceCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((InterfaceDeclarationSyntax)ctx.Node))
            .Where(static symbol => symbol is not null);

        var actorCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node))
            .Where(static symbol => symbol is not null);

        var upcasterCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (ctx, _) => (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node))
            .Where(static symbol => symbol is not null);

        var stateCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (ctx, _) => (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node))
            .Where(static symbol => symbol is not null);

        var sourceManifest = interfaceCandidates.Collect()
            .Combine(actorCandidates.Collect())
            .Combine(upcasterCandidates.Collect())
            .Combine(stateCandidates.Collect())
            .Combine(knownSymbols)
            .Select((input, _) =>
            {
                var ((((interfaces, actors), upcasters), states), known) = input;
                try
                {
                    return BuildManifest(interfaces!, actors!, upcasters!, states!, known);
                }
                catch (Exception ex)
                {
                    return Manifest.FromError(Flatten(ex));
                }
            });

        var referencedManifest = context.CompilationProvider
            .Combine(knownSymbols)
            .Combine(scanReferences)
            .Select((input, _) =>
            {
                var ((compilation, known), scan) = input;
                if (!scan)
                {
                    return Manifest.Empty;
                }

                var interfaces = new List<INamedTypeSymbol>();
                var actors = new List<INamedTypeSymbol>();
                var upcasters = new List<INamedTypeSymbol>();
                var states = new List<INamedTypeSymbol>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    foreach (var type in EnumerateTypes(assembly.GlobalNamespace))
                    {
                        if (type.TypeKind == TypeKind.Interface)
                        {
                            interfaces.Add(type);
                        }
                        else if (type.TypeKind == TypeKind.Class)
                        {
                            actors.Add(type);
                            upcasters.Add(type);
                        }
                    }
                }

                try
                {
                    return BuildManifest(interfaces.ToImmutableArray(), actors.ToImmutableArray(), upcasters.ToImmutableArray(), states.ToImmutableArray(), known);
                }
                catch (Exception ex)
                {
                    return Manifest.FromError(Flatten(ex));
                }
            });

        var manifest = sourceManifest.Combine(referencedManifest).Select((input, _) => input.Left.Merge(input.Right));

        context.RegisterSourceOutput(manifest, (sourceProductionContext, discovered) =>
        {
            try
            {
                foreach (var diagnostic in discovered.Diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }

                if (discovered.Error is not null)
                {
                    sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DAPRGEN998",
                            "Dapr Actors Next generator manifest failed",
                            discovered.Error,
                            "Dapr.Actors.Next.SourceGenerators",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        Location.None));
                    return;
                }

                if (discovered.Interfaces.Length == 0 && discovered.Actors.Length == 0 && discovered.Upcasters.Length == 0 && discovered.Families.Length == 0)
                {
                    return;
                }

                sourceProductionContext.AddSource("DaprActorsNext.Generated.g.cs", SourceText.From(Emit(discovered), Encoding.UTF8));
            }
            catch (Exception ex)
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPRGEN999",
                        "Dapr Actors Next generator failed",
                        ex.ToString(),
                        "Dapr.Actors.Next.SourceGenerators",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
            }
        });
    }

    private static Manifest BuildManifest(
        ImmutableArray<INamedTypeSymbol> interfaceCandidates,
        ImmutableArray<INamedTypeSymbol> actorCandidates,
        ImmutableArray<INamedTypeSymbol> upcasterCandidates,
        ImmutableArray<INamedTypeSymbol> stateCandidates,
        KnownSymbols known)
    {
        var interfaces = interfaceCandidates
            .Where(symbol => IsActorInterface(symbol, known) && HasAttribute(symbol, known.GenerateActorClientAttribute, GenerateActorClientAttributeMetadataName))
            .Select(symbol => BuildInterface(symbol, known))
            .Where(item => item is not null)
            .Cast<ActorInterfaceModel>()
            .Distinct(ActorInterfaceModelComparer.Instance)
            .OrderBy(item => item.FullName, StringComparer.Ordinal)
            .ToImmutableArray();

        var interfaceBySymbol = interfaces.ToDictionary(item => item.MetadataName, StringComparer.Ordinal);
        var actors = actorCandidates
            .Where(symbol => !symbol.IsAbstract && HasAttribute(symbol, known.DaprActorAttribute, DaprActorAttributeMetadataName))
            .Select(symbol => BuildActor(symbol, known, interfaceBySymbol))
            .Where(item => item is not null)
            .Cast<ActorModel>()
            .Distinct(ActorModelComparer.Instance)
            .OrderBy(item => item.ActorType, StringComparer.Ordinal)
            .ToImmutableArray();

        var upcasters = upcasterCandidates
            .Where(symbol => !symbol.IsAbstract)
            .SelectMany(symbol => BuildUpcasters(symbol, known))
            .Distinct(UpcasterModelComparer.Instance)
            .OrderBy(item => item.ImplementationType, StringComparer.Ordinal)
            .ToImmutableArray();

        var states = stateCandidates
            .Concat(upcasters.SelectMany(static upcaster => new[] { upcaster.From, upcaster.To }))
            .Where(symbol => symbol.TypeParameters.Length == 0 && !symbol.IsAbstract && !IsActorInterface(symbol, known) && !ImplementsUpcaster(symbol, known))
            .Select(BuildStateType)
            .Where(static state => state is not null)
            .Cast<StateTypeModel>()
            .Distinct(StateTypeModelComparer.Instance)
            .OrderBy(static state => state.TypeName, StringComparer.Ordinal)
            .ToImmutableArray();

        var (families, diagnostics) = BuildFamilies(states, upcasters);
        return new Manifest(interfaces, actors, upcasters, families, diagnostics);
    }

    private static ActorInterfaceModel? BuildInterface(INamedTypeSymbol symbol, KnownSymbols known)
    {
        if (symbol.TypeParameters.Length > 0)
        {
            return null;
        }

        var methods = GetActorMethods(symbol)
            .Select(method => BuildMethod(method, known))
            .Where(method => method is not null)
            .Cast<ActorMethodModel>()
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        return new ActorInterfaceModel(
            FullName: TypeName(symbol),
            MetadataName: symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Name: symbol.Name,
            Accessibility: symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            ProxyName: "Generated" + TrimInterfacePrefix(symbol.Name) + "Proxy",
            Methods: methods);
    }

    private static ActorMethodModel? BuildMethod(IMethodSymbol method, KnownSymbols known)
    {
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return null;
        }

        var returnKind = ReturnKind(method.ReturnType);
        if (returnKind == MethodReturnKind.Unsupported)
        {
            return null;
        }

        var parameters = method.Parameters.Select((parameter, index) => new ActorParameterModel(
                parameter.Name,
                TypeName(parameter.Type),
                index,
                IsCancellationToken(parameter.Type, known),
                parameter.HasExplicitDefaultValue,
                GetDefaultValue(parameter)))
            .ToImmutableArray();

        var payloadParameters = parameters.Where(parameter => !parameter.IsCancellationToken).ToImmutableArray();
        var namedReturnType = (INamedTypeSymbol)method.ReturnType;
        var returnType = returnKind is MethodReturnKind.TaskOfT or MethodReturnKind.ValueTaskOfT
            ? $"typeof({TypeName(namedReturnType.TypeArguments[0])})"
            : "typeof(void)";

        return new ActorMethodModel(
            method.Name,
            method.Name,
            TypeName(method.ReturnType),
            returnKind,
            returnType,
            parameters,
            payloadParameters,
            "__" + method.Name + "Args");
    }

    private static ActorModel? BuildActor(INamedTypeSymbol symbol, KnownSymbols known, Dictionary<string, ActorInterfaceModel> interfaces)
    {
        var actorInterfaces = symbol.AllInterfaces
            .Where(candidate => IsActorInterface(candidate, known))
            .Select(candidate => candidate.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
            .Where(interfaces.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => interfaces[name])
            .ToImmutableArray();

        if (actorInterfaces.IsEmpty)
        {
            return null;
        }

        var attribute = symbol.GetAttributes().FirstOrDefault(attr => IsAttribute(attr, known.DaprActorAttribute, DaprActorAttributeMetadataName));
        var actorType = attribute?.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value?.ToString()
            : null;
        actorType = string.IsNullOrWhiteSpace(actorType) ? symbol.Name : actorType;
        var contractVersion = 1;
        foreach (var argument in attribute?.NamedArguments ?? ImmutableArray<KeyValuePair<string, TypedConstant>>.Empty)
        {
            if (argument.Key == "ContractVersion" && argument.Value.Value is int value)
            {
                contractVersion = value;
            }
        }

        var constructor = symbol.InstanceConstructors
            .Where(ctor => !ctor.IsStatic && ctor.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(ctor => ctor.Parameters.Length)
            .FirstOrDefault();
        var constructorParameters = constructor?.Parameters
            .Select(parameter => new ConstructorParameterModel(parameter.Name, TypeName(parameter.Type), parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == "Dapr.Actors.Next.Abstractions.ActorId"))
            .ToImmutableArray() ?? ImmutableArray<ConstructorParameterModel>.Empty;

        return new ActorModel(
            actorType!,
            contractVersion,
            TypeName(symbol),
            symbol.Name,
            actorInterfaces,
            symbol.Name + "Dispatcher",
            constructorParameters);
    }

    private static IEnumerable<UpcasterModel> BuildUpcasters(INamedTypeSymbol symbol, KnownSymbols known)
    {
        foreach (var implemented in symbol.AllInterfaces)
        {
            if (!IsUpcasterInterface(implemented, known))
            {
                continue;
            }

            if (implemented.TypeArguments[0] is not INamedTypeSymbol from || implemented.TypeArguments[1] is not INamedTypeSymbol to)
            {
                continue;
            }

            yield return new UpcasterModel(TypeName(symbol), from, to);
        }
    }

    private static StateTypeModel? BuildStateType(INamedTypeSymbol symbol)
    {
        if (!ActorStateMigrationShared.TryParseNumericVersion(symbol.Name, out var simpleCanonicalName, out var version))
        {
            return null;
        }

        var canonicalName = symbol.ContainingNamespace.IsGlobalNamespace
            ? simpleCanonicalName
            : symbol.ContainingNamespace.ToDisplayString() + "." + simpleCanonicalName;
        var members = GetSerializableMembers(symbol)
            .Select(BuildStateMember)
            .OrderBy(static member => member.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        return new StateTypeModel(
            TypeName(symbol),
            symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            symbol.Name,
            canonicalName,
            version,
            ActorStateMigrationShared.ComputeShapeHash(symbol),
            ActorStateMigrationShared.HasPublicParameterlessConstructor(symbol),
            members);
    }

    private static (ImmutableArray<StateFamilyModel> Families, ImmutableArray<Diagnostic> Diagnostics) BuildFamilies(
        ImmutableArray<StateTypeModel> states,
        ImmutableArray<UpcasterModel> upcasters)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var parent = states.ToDictionary(static state => state.TypeName, static state => state.TypeName, StringComparer.Ordinal);

        string Find(string value)
        {
            var current = parent[value];
            if (StringComparer.Ordinal.Equals(current, value))
            {
                return current;
            }

            var root = Find(current);
            parent[value] = root;
            return root;
        }

        void Union(string left, string right)
        {
            if (!parent.ContainsKey(left) || !parent.ContainsKey(right))
            {
                return;
            }

            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (!StringComparer.Ordinal.Equals(leftRoot, rightRoot))
            {
                parent[rightRoot] = leftRoot;
            }
        }

        foreach (var group in states.GroupBy(static state => state.CanonicalName, StringComparer.Ordinal))
        {
            var items = group.OrderBy(static state => state.Version, NumericVersionComparer.Instance).ThenBy(static state => state.TypeName, StringComparer.Ordinal).ToArray();
            for (var i = 1; i < items.Length; i++)
            {
                Union(items[0].TypeName, items[i].TypeName);
            }
        }

        foreach (var upcaster in upcasters)
        {
            Union(TypeName(upcaster.From), TypeName(upcaster.To));
        }

        var families = ImmutableArray.CreateBuilder<StateFamilyModel>();
        foreach (var component in states.GroupBy(state => Find(state.TypeName), StringComparer.Ordinal))
        {
            var componentStates = component
                .OrderBy(static state => state.CanonicalName, StringComparer.Ordinal)
                .ThenBy(static state => state.Version, NumericVersionComparer.Instance)
                .ThenBy(static state => state.TypeName, StringComparer.Ordinal)
                .ToImmutableArray();
            if (componentStates.Length < 2)
            {
                continue;
            }

            var nodes = componentStates
                .Select((state, index) => new StateNodeModel(index, state))
                .ToImmutableArray();
            var indexByType = nodes.ToDictionary(static node => node.State.TypeName, static node => node.Index, StringComparer.Ordinal);
            var explicitEdges = upcasters
                .Where(upcaster => indexByType.ContainsKey(TypeName(upcaster.From)) && indexByType.ContainsKey(TypeName(upcaster.To)))
                .Select(upcaster => new StateEdgeModel(
                    indexByType[TypeName(upcaster.From)],
                    indexByType[TypeName(upcaster.To)],
                    TypeName(upcaster.From),
                    TypeName(upcaster.To),
                    upcaster.ImplementationType,
                    IsGenerated: false,
                    ImmutableArray<StateMemberModel>.Empty))
                .ToImmutableArray();

            var edges = explicitEdges.ToBuilder();
            for (var i = 0; i < nodes.Length - 1; i++)
            {
                if (edges.Any(edge => edge.FromIndex == i && edge.ToIndex == i + 1))
                {
                    continue;
                }

                var from = nodes[i].State;
                var to = nodes[i + 1].State;
                if (TryBuildAdditiveHop(from, to, out var copiedMembers))
                {
                    edges.Add(new StateEdgeModel(i, i + 1, from.TypeName, to.TypeName, null, IsGenerated: true, copiedMembers));
                }
            }

            var orderedEdges = edges
                .OrderBy(static edge => edge.FromIndex)
                .ThenBy(static edge => edge.ToIndex)
                .ThenBy(static edge => edge.UpcasterType ?? string.Empty, StringComparer.Ordinal)
                .ToImmutableArray();

            var canonicalName = componentStates.Select(static state => state.CanonicalName).OrderBy(static name => name, StringComparer.Ordinal).First();
            if (!IsAppendOnly(nodes, orderedEdges))
            {
                diagnostics.Add(Diagnostic.Create(NonAppendOnlyChainDiagnostic, Location.None, canonicalName));
                continue;
            }

            if (HasNonUniquePath(nodes, orderedEdges))
            {
                diagnostics.Add(Diagnostic.Create(NonUniqueFoldPathDiagnostic, Location.None, canonicalName));
                continue;
            }

            if (!HasContiguousPath(nodes, orderedEdges))
            {
                continue;
            }

            families.Add(new StateFamilyModel(canonicalName, nodes, orderedEdges));
        }

        return (
            families.OrderBy(static family => family.CanonicalName, StringComparer.Ordinal).ToImmutableArray(),
            diagnostics.ToImmutable());
    }

    private static bool TryBuildAdditiveHop(StateTypeModel from, StateTypeModel to, out ImmutableArray<StateMemberModel> copiedMembers)
    {
        copiedMembers = ImmutableArray<StateMemberModel>.Empty;
        if (!to.HasPublicParameterlessConstructor)
        {
            return false;
        }

        var fromMembers = from.Members.ToDictionary(static member => member.Name, StringComparer.Ordinal);
        var copied = ImmutableArray.CreateBuilder<StateMemberModel>();
        foreach (var toMember in to.Members)
        {
            if (!fromMembers.TryGetValue(toMember.Name, out var fromMember))
            {
                if (toMember.IsRequired)
                {
                    return false;
                }

                continue;
            }

            if (!StringComparer.Ordinal.Equals(fromMember.TypeName, toMember.TypeName) || !toMember.CanWrite)
            {
                return false;
            }

            copied.Add(toMember);
        }

        foreach (var fromMember in from.Members)
        {
            if (!to.Members.Any(member => StringComparer.Ordinal.Equals(member.Name, fromMember.Name)))
            {
                return false;
            }
        }

        copiedMembers = copied.OrderBy(static member => member.Name, StringComparer.Ordinal).ToImmutableArray();
        return true;
    }

    private static bool IsAppendOnly(ImmutableArray<StateNodeModel> nodes, ImmutableArray<StateEdgeModel> edges) =>
        edges.All(static edge => edge.ToIndex == edge.FromIndex + 1)
        && edges.Select(static edge => edge.FromIndex).Distinct().Count() == edges.Length
        && edges.Select(static edge => edge.ToIndex).Distinct().Count() == edges.Length
        && edges.Length == nodes.Length - 1;

    private static bool HasContiguousPath(ImmutableArray<StateNodeModel> nodes, ImmutableArray<StateEdgeModel> edges)
    {
        for (var i = 0; i < nodes.Length - 1; i++)
        {
            if (!edges.Any(edge => edge.FromIndex == i && edge.ToIndex == i + 1))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasNonUniquePath(ImmutableArray<StateNodeModel> nodes, ImmutableArray<StateEdgeModel> edges)
    {
        foreach (var source in nodes)
        {
            foreach (var target in nodes)
            {
                if (source.Index >= target.Index)
                {
                    continue;
                }

                var count = CountPaths(source.Index, target.Index, edges, new HashSet<int>());
                if (count > 1)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountPaths(int current, int target, ImmutableArray<StateEdgeModel> edges, HashSet<int> seen)
    {
        if (!seen.Add(current))
        {
            return 0;
        }

        var count = 0;
        foreach (var edge in edges.Where(edge => edge.FromIndex == current))
        {
            count += edge.ToIndex == target ? 1 : CountPaths(edge.ToIndex, target, edges, seen);
            if (count > 1)
            {
                break;
            }
        }

        seen.Remove(current);
        return count;
    }

    private static StateMemberModel BuildStateMember(ISymbol symbol)
    {
        if (symbol is IPropertySymbol property)
        {
            return new StateMemberModel(
                property.Name,
                TypeName(property.Type),
                property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                property.SetMethod is { DeclaredAccessibility: Accessibility.Public },
                property.IsRequired);
        }

        var field = (IFieldSymbol)symbol;
        return new StateMemberModel(
            field.Name,
            TypeName(field.Type),
            field.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            !field.IsReadOnly,
            field.IsRequired);
    }

    private static IEnumerable<ISymbol> GetSerializableMembers(INamedTypeSymbol type)
    {
        foreach (var property in type.GetMembers()
                     .OfType<IPropertySymbol>()
                     .Where(static property => !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null && property.Parameters.Length == 0)
                     .OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
            yield return property;
        }

        foreach (var field in type.GetMembers()
                     .OfType<IFieldSymbol>()
                     .Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
                     .OrderBy(static field => field.Name, StringComparer.Ordinal))
        {
            yield return field;
        }
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
    {
        if (type.IsValueType)
        {
            return true;
        }

        return type.InstanceConstructors.Any(static ctor => !ctor.IsStatic && ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public);
    }

    private static string ComputeShapeHash(INamedTypeSymbol symbol) => ActorStateMigrationShared.ComputeShapeHash(symbol);

    private static void AppendType(StringBuilder builder, ITypeSymbol type, HashSet<string> seen)
    {
        var identity = TypeIdentity(type);
        builder.Append("type:").Append(identity).Append(';');
        if (!seen.Add(identity))
        {
            builder.Append("recursive;");
            return;
        }

        if (IsLeaf(type))
        {
            seen.Remove(identity);
            return;
        }

        if (type is INamedTypeSymbol named)
        {
            foreach (var member in GetSerializableMembers(named))
            {
                builder.Append("member:").Append(member.Name).Append(':');
                AppendMemberType(builder, GetMemberType(member), seen);
                builder.Append(';');
            }
        }

        seen.Remove(identity);
    }

    private static void AppendMemberType(StringBuilder builder, ITypeSymbol type, HashSet<string> seen)
    {
        if (type is IArrayTypeSymbol array)
        {
            builder.Append("array[");
            AppendMemberType(builder, array.ElementType, seen);
            builder.Append(']');
            return;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } generic)
        {
            builder.Append(TypeIdentity(generic.ConstructedFrom)).Append('<');
            foreach (var argument in generic.TypeArguments)
            {
                AppendMemberType(builder, argument, seen);
                builder.Append(',');
            }

            builder.Append('>');
            return;
        }

        AppendType(builder, type, seen);
    }

    private static ITypeSymbol GetMemberType(ISymbol member) =>
        member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => throw new InvalidOperationException($"Unsupported member '{member.Name}'."),
        };

    private static string TypeIdentity(ITypeSymbol type)
    {
        var assembly = type.ContainingAssembly?.Identity.GetDisplayName();
        var name = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return string.IsNullOrEmpty(assembly) ? name : name + ", " + assembly;
    }

    private static bool IsLeaf(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Boolean
            or SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Char
            or SpecialType.System_String
            or SpecialType.System_Decimal
        || type.TypeKind == TypeKind.Enum
        || type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) is "System.DateTime" or "System.DateTimeOffset" or "System.TimeSpan" or "System.Guid";

    private static string ToLowerHex(byte[] bytes, int length)
    {
        const string Hex = "0123456789abcdef";
        var chars = new char[length * 2];
        for (var i = 0; i < length; i++)
        {
            chars[i * 2] = Hex[bytes[i] >> 4];
            chars[i * 2 + 1] = Hex[bytes[i] & 0xF];
        }

        return new string(chars);
    }

    private static bool TryParseNumericVersion(string typeName, out string canonicalName, out string version) =>
        ActorStateMigrationShared.TryParseNumericVersion(typeName, out canonicalName, out version);

    private static bool ImplementsUpcaster(INamedTypeSymbol symbol, KnownSymbols known) =>
        symbol.AllInterfaces.Any(candidate => IsUpcasterInterface(candidate, known));

    private static string Emit(Manifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Dapr.Actors.Next.Generated");
        sb.AppendLine("{");
        EmitProxyFactory(sb, manifest);
        foreach (var actorInterface in manifest.Interfaces)
        {
            EmitProxy(sb, actorInterface);
        }

        foreach (var actor in manifest.Actors)
        {
            EmitDispatcher(sb, actor);
        }

        EmitRegistry(sb, manifest);
        EmitJsonContext(sb, manifest);
        EmitModule(sb, manifest);
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitProxyFactory(StringBuilder sb, Manifest manifest)
    {
        sb.AppendLine("    internal sealed class GeneratedActorProxyFactory : global::Dapr.Actors.Next.Core.Client.IActorProxyFactory");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::Dapr.Actors.Next.Core.Client.IActorInvocationClient invocationClient;");
        sb.AppendLine("        private readonly global::Dapr.Actors.Next.Core.Serialization.IActorWireSerializer wireSerializer;");
        sb.AppendLine("        public GeneratedActorProxyFactory(global::Dapr.Actors.Next.Core.Client.IActorInvocationClient invocationClient, global::Dapr.Actors.Next.Core.Serialization.IActorWireSerializer wireSerializer)");
        sb.AppendLine("        {");
        sb.AppendLine("            this.invocationClient = invocationClient;");
        sb.AppendLine("            this.wireSerializer = wireSerializer;");
        sb.AppendLine("        }");
        sb.AppendLine("        public TActor Create<TActor>(global::Dapr.Actors.Next.Abstractions.ActorId actorId, string actorType) where TActor : global::Dapr.Actors.Next.Abstractions.IActor");
        sb.AppendLine("        {");
        foreach (var actorInterface in manifest.Interfaces)
        {
            sb.AppendLine($"            if (typeof(TActor) == typeof({actorInterface.FullName})) return (TActor)(object)new {actorInterface.ProxyName}(actorId, actorType, invocationClient, wireSerializer);");
        }

        sb.AppendLine("            throw new global::System.InvalidOperationException($\"Actor interface '{typeof(TActor).FullName}' is not generated in this assembly.\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitProxy(StringBuilder sb, ActorInterfaceModel actorInterface)
    {
        sb.AppendLine($"    {actorInterface.Accessibility} sealed class {actorInterface.ProxyName} : {actorInterface.FullName}");
        sb.AppendLine("    {");
        sb.AppendLine("        private static readonly global::System.Collections.Generic.IReadOnlyDictionary<string, string> EmptyHeaders = global::Dapr.Actors.Next.Core.ActorHeaders.Empty;");
        sb.AppendLine("        private readonly global::Dapr.Actors.Next.Abstractions.ActorId actorId;");
        sb.AppendLine("        private readonly string actorType;");
        sb.AppendLine("        private readonly global::Dapr.Actors.Next.Core.Client.IActorInvocationClient invocationClient;");
        sb.AppendLine("        private readonly global::Dapr.Actors.Next.Core.Serialization.IActorWireSerializer wireSerializer;");
        sb.AppendLine($"        public {actorInterface.ProxyName}(global::Dapr.Actors.Next.Abstractions.ActorId actorId, string actorType, global::Dapr.Actors.Next.Core.Client.IActorInvocationClient invocationClient, global::Dapr.Actors.Next.Core.Serialization.IActorWireSerializer wireSerializer)");
        sb.AppendLine("        {");
        sb.AppendLine("            this.actorId = actorId;");
        sb.AppendLine("            this.actorType = actorType;");
        sb.AppendLine("            this.invocationClient = invocationClient;");
        sb.AppendLine("            this.wireSerializer = wireSerializer;");
        sb.AppendLine("        }");
        foreach (var method in actorInterface.Methods)
        {
            EmitProxyMethod(sb, method);
        }

        foreach (var method in actorInterface.Methods.Where(method => method.PayloadParameters.Length > 1))
        {
            EmitArgsRecord(sb, method);
        }

        EmitProxyHelpers(sb);
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitProxyMethod(StringBuilder sb, ActorMethodModel method)
    {
        var signature = string.Join(", ", method.Parameters.Select(FormatParameter));
        sb.AppendLine($"        public {method.ReturnType} {method.Name}({signature})");
        sb.AppendLine("        {");
        var cancellationToken = method.Parameters.FirstOrDefault(parameter => parameter.IsCancellationToken)?.Name ?? "default";
        if (method.PayloadParameters.Length == 0)
        {
            sb.AppendLine("            var payload = global::System.ReadOnlyMemory<byte>.Empty;");
        }
        else if (method.PayloadParameters.Length == 1)
        {
            var parameter = method.PayloadParameters[0];
            sb.AppendLine($"            var payload = wireSerializer.SerializeToBytes<{parameter.TypeName}>({parameter.Name});");
        }
        else
        {
            sb.AppendLine($"            var payload = wireSerializer.SerializeToBytes(new {method.ArgsTypeName}({string.Join(", ", method.PayloadParameters.Select(parameter => parameter.Name))}));");
        }

        if (method.ReturnKind is MethodReturnKind.Task or MethodReturnKind.ValueTask)
        {
            var invocation = $"InvokeVoidAsync({Literal(method.WireName)}, payload, {cancellationToken})";
            if (method.ReturnKind is MethodReturnKind.Task)
            {
                sb.AppendLine($"            return {invocation};");
            }
            else
            {
                sb.AppendLine($"            return new global::System.Threading.Tasks.ValueTask({invocation});");
            }

            sb.AppendLine("        }");
            return;
        }

        var resultType = method.ReturnType.Substring(method.ReturnType.IndexOf('<') + 1).TrimEnd('>');
        var resultInvocation = $"InvokeResultAsync<{resultType}>({Literal(method.WireName)}, payload, {cancellationToken})";
        if (method.ReturnKind is MethodReturnKind.TaskOfT)
        {
            sb.AppendLine($"            return {resultInvocation};");
        }
        else
        {
            sb.AppendLine($"            return new global::System.Threading.Tasks.ValueTask<{resultType}>({resultInvocation});");
        }

        sb.AppendLine("        }");
    }

    private static void EmitProxyHelpers(StringBuilder sb)
    {
        sb.AppendLine("        private async global::System.Threading.Tasks.Task InvokeVoidAsync(string methodName, global::System.ReadOnlyMemory<byte> payload, global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine("            await invocationClient.InvokeAsync(actorType, actorId.Value, methodName, payload, EmptyHeaders, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private async global::System.Threading.Tasks.Task<TResult> InvokeResultAsync<TResult>(string methodName, global::System.ReadOnlyMemory<byte> payload, global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine("            var response = await invocationClient.InvokeAsync(actorType, actorId.Value, methodName, payload, EmptyHeaders, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("            return wireSerializer.DeserializeFromBytes<TResult>(response is null ? global::System.ReadOnlyMemory<byte>.Empty : response)!;");
        sb.AppendLine("        }");
    }

    private static void EmitDispatcher(StringBuilder sb, ActorModel actor)
    {
        sb.AppendLine($"    internal sealed class {actor.DispatcherName} : global::Dapr.Actors.Next.Abstractions.Dispatching.IActorDispatcher");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::Dapr.Actors.Next.Core.Serialization.IActorWireSerializer wireSerializer;");
        sb.AppendLine($"        public {actor.DispatcherName}(global::Dapr.Actors.Next.Core.Serialization.IActorWireSerializer wireSerializer) => this.wireSerializer = wireSerializer;");
        foreach (var method in actor.DispatchMethods.Where(method => method.PayloadParameters.Length > 1))
        {
            EmitArgsRecord(sb, method);
        }

        sb.AppendLine("        public global::System.Threading.Tasks.ValueTask<global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse> DispatchAsync(global::Dapr.Actors.Next.Abstractions.IActor actor, global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchRequest request, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var typed = ({actor.ImplementationType})actor;");
        sb.AppendLine("            switch (request.MethodName)");
        sb.AppendLine("            {");
        foreach (var method in actor.DispatchMethods)
        {
            sb.AppendLine($"                case {Literal(method.WireName)}:");
            sb.AppendLine("                {");
            EmitDispatcherCase(sb, method);
            sb.AppendLine("                }");
        }

        sb.AppendLine("                default:");
        sb.AppendLine("                    return global::System.Threading.Tasks.ValueTask.FromException<global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse>(new global::System.InvalidOperationException($\"Unknown actor method '{request.MethodName}'.\"));");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        EmitDispatcherHelpers(sb);
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitDispatcherCase(StringBuilder sb, ActorMethodModel method)
    {
        if (method.PayloadParameters.Length == 1)
        {
            var parameter = method.PayloadParameters[0];
            sb.AppendLine($"                    var {parameter.Name} = wireSerializer.DeserializeFromBytes<{parameter.TypeName}>(request.Payload)!;");
        }
        else if (method.PayloadParameters.Length > 1)
        {
            sb.AppendLine($"                    var args = wireSerializer.DeserializeFromBytes<{method.ArgsTypeName}>(request.Payload);");
            foreach (var parameter in method.PayloadParameters)
            {
                sb.AppendLine($"                    var {parameter.Name} = args.{parameter.Name};");
            }
        }

        var arguments = string.Join(", ", method.Parameters.Select(parameter => parameter.IsCancellationToken ? "cancellationToken" : parameter.Name));
        if (method.ReturnKind is MethodReturnKind.Task or MethodReturnKind.ValueTask)
        {
            sb.AppendLine($"                    return CompleteVoidAsync(typed.{method.Name}({arguments}));");
        }
        else
        {
            var resultType = method.ReturnType.Substring(method.ReturnType.IndexOf('<') + 1).TrimEnd('>');
            sb.AppendLine($"                    return CompleteResultAsync<{resultType}>(typed.{method.Name}({arguments}));");
        }
    }

    private static void EmitDispatcherHelpers(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("        private static async global::System.Threading.Tasks.ValueTask<global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse> CompleteVoidAsync(global::System.Threading.Tasks.Task task)");
        sb.AppendLine("        {");
        sb.AppendLine("            await task.ConfigureAwait(false);");
        sb.AppendLine("            return new global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse(null);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static async global::System.Threading.Tasks.ValueTask<global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse> CompleteVoidAsync(global::System.Threading.Tasks.ValueTask task)");
        sb.AppendLine("        {");
        sb.AppendLine("            await task.ConfigureAwait(false);");
        sb.AppendLine("            return new global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse(null);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private async global::System.Threading.Tasks.ValueTask<global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse> CompleteResultAsync<TResult>(global::System.Threading.Tasks.Task<TResult> task)");
        sb.AppendLine("        {");
        sb.AppendLine("            var result = await task.ConfigureAwait(false);");
        sb.AppendLine("            return new global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse(wireSerializer.SerializeToBytes<TResult>(result));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private async global::System.Threading.Tasks.ValueTask<global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse> CompleteResultAsync<TResult>(global::System.Threading.Tasks.ValueTask<TResult> task)");
        sb.AppendLine("        {");
        sb.AppendLine("            var result = await task.ConfigureAwait(false);");
        sb.AppendLine("            return new global::Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchResponse(wireSerializer.SerializeToBytes<TResult>(result));");
        sb.AppendLine("        }");
    }

    private static void EmitRegistry(StringBuilder sb, Manifest manifest)
    {
        sb.AppendLine("    internal sealed class GeneratedActorRegistry : global::Dapr.Actors.Next.Abstractions.Registry.IActorRegistry");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::System.Collections.Generic.IReadOnlyList<global::Dapr.Actors.Next.Abstractions.Registry.ActorTypeDescriptor> actors;");
        sb.AppendLine("        public GeneratedActorRegistry(global::Dapr.Actors.Next.Abstractions.Options.DaprActorsOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            var actorList = new global::System.Collections.Generic.List<global::Dapr.Actors.Next.Abstractions.Registry.ActorTypeDescriptor>();");
        foreach (var actor in manifest.Actors)
        {
            sb.AppendLine($"            var {actor.DispatcherName}ExplicitRegistration = options.Actors.Find(typeof({actor.ImplementationType}));");
            sb.AppendLine($"            if (options.EnableAutoActorRegistration || {actor.DispatcherName}ExplicitRegistration is not null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var actorType = {actor.DispatcherName}ExplicitRegistration?.ActorTypeName ?? {Literal(actor.ActorType)};");
            sb.AppendLine($"                actorList.Add(new global::Dapr.Actors.Next.Abstractions.Registry.ActorTypeDescriptor(actorType, {actor.ContractVersion}, typeof({actor.ImplementationType}), typeof({actor.PrimaryInterface.FullName}), new global::System.Collections.Generic.List<global::Dapr.Actors.Next.Abstractions.Registry.ActorMethodDescriptor>");
            sb.AppendLine("                {");
            foreach (var method in actor.DispatchMethods)
            {
                    sb.AppendLine($"                    new global::Dapr.Actors.Next.Abstractions.Registry.ActorMethodDescriptor({Literal(method.Name)}, {Literal(method.WireName)}, {method.ReturnTypeExpression}, new global::System.Collections.Generic.List<global::Dapr.Actors.Next.Abstractions.Registry.ActorParameterDescriptor>");
                sb.AppendLine("                    {");
                foreach (var parameter in method.Parameters)
                {
                    sb.AppendLine($"                        new global::Dapr.Actors.Next.Abstractions.Registry.ActorParameterDescriptor({Literal(parameter.Name)}, typeof({parameter.TypeName}), {parameter.Position}, {Bool(parameter.IsCancellationToken)}, {Bool(parameter.HasDefaultValue)}, null),");
                }
                sb.AppendLine("                    }),");
            }
            sb.AppendLine("                }));");
            sb.AppendLine("            }");
        }

        sb.AppendLine("            actors = actorList;");
        sb.AppendLine("        }");
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::Dapr.Actors.Next.Abstractions.Registry.ActorTypeDescriptor> Actors => actors;");
        sb.AppendLine("        public bool TryGet(string actorType, out global::Dapr.Actors.Next.Abstractions.Registry.ActorTypeDescriptor descriptor)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var actor in actors)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (global::System.StringComparer.Ordinal.Equals(actor.ActorType, actorType))");
        sb.AppendLine("                {");
        sb.AppendLine("                    descriptor = actor;");
        sb.AppendLine("                    return true;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            descriptor = null!;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitJsonContext(StringBuilder sb, Manifest manifest)
    {
        var actorPayloadTypes = manifest.Actors
            .SelectMany(actor => actor.DispatchMethods)
            .SelectMany(method => method.PayloadParameters.Select(parameter => parameter.TypeName).Concat(method.ReturnKind is MethodReturnKind.TaskOfT or MethodReturnKind.ValueTaskOfT ? new[] { method.ReturnType.Substring(method.ReturnType.IndexOf('<') + 1).TrimEnd('>') } : Enumerable.Empty<string>()))
            .Where(type => type != "void");
        var statePayloadTypes = manifest.Families
            .SelectMany(static family => family.Nodes)
            .Select(static node => node.State.TypeName);
        var payloadTypes = actorPayloadTypes
            .Concat(statePayloadTypes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();
        foreach (var type in payloadTypes)
        {
            sb.AppendLine($"    [global::System.Text.Json.Serialization.JsonSerializable(typeof({type}))]");
        }

        foreach (var type in statePayloadTypes.Distinct(StringComparer.Ordinal).OrderBy(type => type, StringComparer.Ordinal))
        {
            sb.AppendLine($"    [global::System.Text.Json.Serialization.JsonSerializable(typeof(global::Dapr.Actors.Next.Abstractions.State.ActorStateEnvelope<{type}>))]");
            sb.AppendLine($"    [global::System.Text.Json.Serialization.JsonSerializable(typeof(global::Dapr.Actors.Next.Abstractions.State.ActorStatePlainEnvelope<{type}>))]");
        }

        sb.AppendLine("    internal partial class DaprActorsJsonSerializerContext : global::System.Text.Json.Serialization.JsonSerializerContext");
        sb.AppendLine("    {");
        sb.AppendLine("        public DaprActorsJsonSerializerContext() : base(null)");
        sb.AppendLine("        {");
        sb.AppendLine("        }");
        sb.AppendLine("        protected override global::System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;");
        sb.AppendLine("        public override global::System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(global::System.Type type) => null;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitModule(StringBuilder sb, Manifest manifest)
    {
        sb.AppendLine("    internal static class GeneratedActorRegistrationModule");
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void Register() => global::Dapr.Actors.Next.Abstractions.Options.DaprActorsGeneratedRegistration.Register(RegisterServices);");
        sb.AppendLine("        private static void RegisterServices(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::Dapr.Actors.Next.Abstractions.Options.DaprActorsOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<GeneratedActorProxyFactory>(services);");
        sb.AppendLine("            global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::Dapr.Actors.Next.Core.Client.IActorProxyFactory>(services, sp => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<GeneratedActorProxyFactory>(sp));");
        sb.AppendLine("            global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::Dapr.Actors.Next.Abstractions.Registry.IActorRegistry>(services, new GeneratedActorRegistry(options));");
        foreach (var actor in manifest.Actors)
        {
            sb.AppendLine($"            global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<{actor.DispatcherName}>(services);");
        }

        if (manifest.Upcasters.Length > 0 || manifest.Families.Length > 0)
        {
            sb.AppendLine("            if (options.EnableAutoStateMigrationRegistration)");
            sb.AppendLine("            {");
            foreach (var upcaster in manifest.Upcasters)
            {
                sb.AppendLine($"                global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<{upcaster.FromType}, {upcaster.ToType}>, {upcaster.ImplementationType}>(services);");
            }

            if (manifest.Families.Length > 0)
            {
                sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<global::Dapr.Actors.Next.Abstractions.State.Versioning.IActorStateMigrator>(services, sp => new global::Dapr.Actors.Next.Core.State.Versioning.ActorStateMigrationRegistry(BuildStateMigrationFamilies(sp)));");
            }

            sb.AppendLine("            }");
        }

        sb.AppendLine("            global::Dapr.Actors.Next.Core.DependencyInjection.DaprActorsCoreServiceCollectionExtensions.AddDaprActorsCore(services, builder =>");
        sb.AppendLine("            {");
        foreach (var actor in manifest.Actors)
        {
            var constructorArguments = actor.ConstructorParameters.Length == 0
                ? string.Empty
                : string.Join(", ", actor.ConstructorParameters.Select(parameter => parameter.IsActorId ? "actorId" : $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{parameter.TypeName}>(sp)"));
            sb.AppendLine($"                var {actor.DispatcherName}ExplicitRegistration = options.Actors.Find(typeof({actor.ImplementationType}));");
            sb.AppendLine($"                if (options.EnableAutoActorRegistration || {actor.DispatcherName}ExplicitRegistration is not null)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var actorType = {actor.DispatcherName}ExplicitRegistration?.ActorTypeName ?? {Literal(actor.ActorType)};");
            sb.AppendLine($"                    builder.Add(actorType, typeof({actor.PrimaryInterface.FullName}), typeof({actor.ImplementationType}),");
            sb.AppendLine($"                    (sp, actorId) => new {actor.ImplementationType}({constructorArguments}),");
            sb.AppendLine($"                    sp => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{actor.DispatcherName}>(sp),");
            sb.AppendLine("                    new global::Dapr.Actors.Next.Core.Activation.ActorLifecycle(");
            sb.AppendLine("                        static (actor, ct) => ((global::Dapr.Actors.Next.Abstractions.Actor)actor).InvokeOnActivateAsync(ct),");
            sb.AppendLine("                        static (actor, ct) => ((global::Dapr.Actors.Next.Abstractions.Actor)actor).InvokeOnDeactivateAsync(ct),");
            sb.AppendLine("                        static (actor, context, ct) => ((global::Dapr.Actors.Next.Abstractions.Actor)actor).InvokeOnPreActorMethodAsync(context, ct),");
            sb.AppendLine("                        static (actor, context, exception, ct) => ((global::Dapr.Actors.Next.Abstractions.Actor)actor).InvokeOnPostActorMethodAsync(context, exception, ct)),");
            sb.AppendLine("                    options: options);");
            sb.AppendLine("                }");
        }

        sb.AppendLine("            });");
        sb.AppendLine("        }");
        if (manifest.Families.Length > 0)
        {
            EmitStateMigrationHelpers(sb, manifest);
        }

        sb.AppendLine("    }");
    }

    private static void EmitStateMigrationHelpers(StringBuilder sb, Manifest manifest)
    {
        sb.AppendLine();
        sb.AppendLine("        private static global::System.Collections.Generic.IReadOnlyList<global::Dapr.Actors.Next.Core.State.Versioning.ActorStateMigrationFamilyRegistration> BuildStateMigrationFamilies(global::System.IServiceProvider serviceProvider)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new global::Dapr.Actors.Next.Core.State.Versioning.ActorStateMigrationFamilyRegistration[]");
        sb.AppendLine("            {");
        foreach (var family in manifest.Families)
        {
            sb.AppendLine("                new global::Dapr.Actors.Next.Core.State.Versioning.ActorStateMigrationFamilyRegistration(");
            sb.AppendLine($"                    new global::Dapr.Actors.Next.Abstractions.State.Versioning.ActorStateMigrationFamily({Literal(family.CanonicalName)},");
            sb.AppendLine("                        new global::Dapr.Actors.Next.Abstractions.State.Versioning.ActorStateMigrationNode[]");
            sb.AppendLine("                        {");
            foreach (var node in family.Nodes)
            {
                sb.AppendLine($"                            new global::Dapr.Actors.Next.Abstractions.State.Versioning.ActorStateMigrationNode({node.Index}, typeof({node.State.TypeName}), {Literal(node.State.ShapeHash)}),");
            }

            sb.AppendLine("                        },");
            sb.AppendLine("                        new global::Dapr.Actors.Next.Abstractions.State.Versioning.ActorStateMigrationEdge[]");
            sb.AppendLine("                        {");
            foreach (var edge in family.Edges)
            {
                var upcasterType = edge.UpcasterType is null ? "null" : $"typeof({edge.UpcasterType})";
                sb.AppendLine($"                            new global::Dapr.Actors.Next.Abstractions.State.Versioning.ActorStateMigrationEdge({edge.FromIndex}, {edge.ToIndex}, {upcasterType}),");
            }

            sb.AppendLine("                        }),");
            sb.AppendLine("                    new global::Dapr.Actors.Next.Core.State.Versioning.ActorStateNodeDeserializer[]");
            sb.AppendLine("                    {");
            foreach (var node in family.Nodes)
            {
                sb.AppendLine($"                        new global::Dapr.Actors.Next.Core.State.Versioning.ActorStateNodeDeserializer({node.Index}, DeserializeStateNode_{node.MethodSuffix}),");
            }

            sb.AppendLine("                    },");
            sb.AppendLine("                    new global::Dapr.Actors.Next.Core.State.Versioning.ActorStateHopRegistration[]");
            sb.AppendLine("                    {");
            foreach (var edge in family.Edges)
            {
                var method = edge.IsGenerated ? $"UpcastStateGenerated_{family.MethodSuffix}_{edge.FromIndex}_{edge.ToIndex}" : $"UpcastStateAuthored_{family.MethodSuffix}_{edge.FromIndex}_{edge.ToIndex}";
                sb.AppendLine($"                        new global::Dapr.Actors.Next.Core.State.Versioning.ActorStateHopRegistration({edge.FromIndex}, {edge.ToIndex}, (state, cancellationToken) => {method}(serviceProvider, state, cancellationToken)),");
            }

            sb.AppendLine("                    }),");
        }

        sb.AppendLine("            };");
        sb.AppendLine("        }");

        foreach (var family in manifest.Families)
        {
            foreach (var node in family.Nodes)
            {
                sb.AppendLine();
                sb.AppendLine($"        private static object? DeserializeStateNode_{node.MethodSuffix}(global::System.ReadOnlyMemory<byte> payload, global::Dapr.Actors.Next.Abstractions.State.Versioning.IActorStateMigrationSerializer serializer)");
                sb.AppendLine("        {");
                sb.AppendLine($"            var envelope = serializer.DeserializeFromBytes<global::Dapr.Actors.Next.Abstractions.State.ActorStateEnvelope<{node.State.TypeName}>>(payload);");
                sb.AppendLine($"            return envelope is null ? serializer.DeserializeFromBytes<{node.State.TypeName}>(payload) : envelope.Value;");
                sb.AppendLine("        }");
            }

            foreach (var edge in family.Edges)
            {
                sb.AppendLine();
                if (edge.IsGenerated)
                {
                    sb.AppendLine($"        private static global::System.Threading.Tasks.ValueTask<object> UpcastStateGenerated_{family.MethodSuffix}_{edge.FromIndex}_{edge.ToIndex}(global::System.IServiceProvider serviceProvider, object state, global::System.Threading.CancellationToken cancellationToken)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var source = ({edge.FromType})state;");
                    sb.AppendLine($"            return global::System.Threading.Tasks.ValueTask.FromResult<object>(new {edge.ToType}");
                    sb.AppendLine("            {");
                    foreach (var member in edge.CopiedMembers)
                    {
                        sb.AppendLine($"                {member.Name} = source.{member.Name},");
                    }

                    sb.AppendLine("            });");
                    sb.AppendLine("        }");
                }
                else
                {
                    sb.AppendLine($"        private static async global::System.Threading.Tasks.ValueTask<object> UpcastStateAuthored_{family.MethodSuffix}_{edge.FromIndex}_{edge.ToIndex}(global::System.IServiceProvider serviceProvider, object state, global::System.Threading.CancellationToken cancellationToken)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var upcaster = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<{edge.FromType}, {edge.ToType}>>(serviceProvider);");
                    sb.AppendLine($"            return await upcaster.UpcastAsync(({edge.FromType})state, cancellationToken).ConfigureAwait(false);");
                    sb.AppendLine("        }");
                }
            }
        }
    }

    private static void EmitArgsRecord(StringBuilder sb, ActorMethodModel method)
    {
        sb.AppendLine($"        private readonly record struct {method.ArgsTypeName}({string.Join(", ", method.PayloadParameters.Select(parameter => parameter.TypeName + " " + parameter.Name))});");
    }

    private static IEnumerable<IMethodSymbol> GetActorMethods(INamedTypeSymbol symbol)
    {
        foreach (var inherited in symbol.AllInterfaces)
        {
            foreach (var method in inherited.GetMembers().OfType<IMethodSymbol>())
            {
                yield return method;
            }
        }

        foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            yield return method;
        }
    }

    private static bool IsActorInterface(INamedTypeSymbol symbol, KnownSymbols known) =>
        (known.IActor is not null && SymbolEqualityComparer.Default.Equals(symbol, known.IActor))
        || string.Equals(symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), IActorMetadataName, StringComparison.Ordinal)
        || symbol.AllInterfaces.Any(candidate =>
            known.IActor is not null && SymbolEqualityComparer.Default.Equals(candidate, known.IActor)
            || string.Equals(candidate.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), IActorMetadataName, StringComparison.Ordinal));

    private static bool IsUpcasterInterface(INamedTypeSymbol symbol, KnownSymbols known) =>
        known.Upcaster is not null && SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, known.Upcaster)
        || string.Equals(symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), "Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<TFromType, TToType>", StringComparison.Ordinal);

    private static bool HasAttribute(INamedTypeSymbol symbol, INamedTypeSymbol? attributeSymbol, string metadataName) =>
        symbol.GetAttributes().Any(attribute => IsAttribute(attribute, attributeSymbol, metadataName));

    private static bool IsAttribute(AttributeData attribute, INamedTypeSymbol? attributeSymbol, string metadataName) =>
        (attributeSymbol is not null && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
        || string.Equals(attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), metadataName, StringComparison.Ordinal);

    private static bool IsCancellationToken(ITypeSymbol type, KnownSymbols known) =>
        SymbolEqualityComparer.Default.Equals(type, known.CancellationToken)
        || string.Equals(type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), CancellationTokenMetadataName, StringComparison.Ordinal);

    private static MethodReturnKind ReturnKind(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol named)
        {
            return MethodReturnKind.Unsupported;
        }

        var metadataName = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (metadataName == "System.Threading.Tasks.Task")
        {
            return MethodReturnKind.Task;
        }

        if (metadataName.StartsWith("System.Threading.Tasks.Task<", StringComparison.Ordinal))
        {
            return MethodReturnKind.TaskOfT;
        }

        if (metadataName == "System.Threading.Tasks.ValueTask")
        {
            return MethodReturnKind.ValueTask;
        }

        if (metadataName.StartsWith("System.Threading.Tasks.ValueTask<", StringComparison.Ordinal))
        {
            return MethodReturnKind.ValueTaskOfT;
        }

        return MethodReturnKind.Unsupported;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in EnumerateNestedTypes(type))
            {
                yield return nested;
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var type in EnumerateTypes(child))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in EnumerateNestedTypes(nested))
            {
                yield return child;
            }
        }
    }

    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string TrimInterfacePrefix(string name) => name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? name.Substring(1) : name;

    private static string FormatParameter(ActorParameterModel parameter) =>
        parameter.HasDefaultValue && parameter.IsCancellationToken
            ? $"{parameter.TypeName} {parameter.Name} = default"
            : $"{parameter.TypeName} {parameter.Name}";

    private static object? GetDefaultValue(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        try
        {
            return parameter.ExplicitDefaultValue;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string Literal(string value) => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private static string Bool(bool value) => value ? "true" : "false";

    private static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.Length == 0 ? "State" : builder.ToString();
    }

    private static string Flatten(Exception exception) => exception.ToString().Replace("\r", " ").Replace("\n", " ");

    [ExcludeFromCodeCoverage]
    private sealed record KnownSymbols(
        INamedTypeSymbol? IActor,
        INamedTypeSymbol? DaprActorAttribute,
        INamedTypeSymbol? GenerateActorClientAttribute,
        INamedTypeSymbol? Upcaster,
        INamedTypeSymbol? CancellationToken);

    [ExcludeFromCodeCoverage]
    private sealed record Manifest(
        ImmutableArray<ActorInterfaceModel> Interfaces,
        ImmutableArray<ActorModel> Actors,
        ImmutableArray<UpcasterModel> Upcasters,
        ImmutableArray<StateFamilyModel> Families,
        ImmutableArray<Diagnostic> Diagnostics,
        string? Error = null)
    {
        public static readonly Manifest Empty = new(
            ImmutableArray<ActorInterfaceModel>.Empty,
            ImmutableArray<ActorModel>.Empty,
            ImmutableArray<UpcasterModel>.Empty,
            ImmutableArray<StateFamilyModel>.Empty,
            ImmutableArray<Diagnostic>.Empty);

        public static Manifest FromError(string error) => new(
            ImmutableArray<ActorInterfaceModel>.Empty,
            ImmutableArray<ActorModel>.Empty,
            ImmutableArray<UpcasterModel>.Empty,
            ImmutableArray<StateFamilyModel>.Empty,
            ImmutableArray<Diagnostic>.Empty,
            error);

        public Manifest Merge(Manifest other) => new(
            Interfaces.Concat(other.Interfaces).Distinct(ActorInterfaceModelComparer.Instance).OrderBy(item => item.FullName, StringComparer.Ordinal).ToImmutableArray(),
            Actors.Concat(other.Actors).Distinct(ActorModelComparer.Instance).OrderBy(item => item.ActorType, StringComparer.Ordinal).ToImmutableArray(),
            Upcasters.Concat(other.Upcasters).Distinct(UpcasterModelComparer.Instance).OrderBy(item => item.ImplementationType, StringComparer.Ordinal).ToImmutableArray(),
            Families.Concat(other.Families).Distinct(StateFamilyModelComparer.Instance).OrderBy(item => item.CanonicalName, StringComparer.Ordinal).ToImmutableArray(),
            Diagnostics.Concat(other.Diagnostics).ToImmutableArray(),
            Error ?? other.Error);
    }

    [ExcludeFromCodeCoverage]
    private sealed record ActorInterfaceModel(
        string FullName,
        string MetadataName,
        string Name,
        string Accessibility,
        string ProxyName,
        ImmutableArray<ActorMethodModel> Methods);

    [ExcludeFromCodeCoverage]
    private sealed record ActorModel(
        string ActorType,
        int ContractVersion,
        string ImplementationType,
        string Name,
        ImmutableArray<ActorInterfaceModel> Interfaces,
        string DispatcherName,
        ImmutableArray<ConstructorParameterModel> ConstructorParameters)
    {
        /// <summary>
        /// The representative interface for the actor, used where a single interface type is recorded
        /// (registration and registry metadata). Interfaces are ordered by full name, so this is stable.
        /// </summary>
        public ActorInterfaceModel PrimaryInterface => Interfaces[0];

        /// <summary>
        /// The union of every implemented actor interface's methods, de-duplicated by wire name. The runtime
        /// routes an invocation to a single dispatcher per actor type, so the dispatcher's method switch and the
        /// registry method metadata must cover all interfaces the actor implements.
        /// </summary>
        public ImmutableArray<ActorMethodModel> DispatchMethods => Interfaces
            .SelectMany(actorInterface => actorInterface.Methods)
            .GroupBy(method => method.WireName, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    [ExcludeFromCodeCoverage]
    private sealed record ActorMethodModel(
        string Name,
        string WireName,
        string ReturnType,
        MethodReturnKind ReturnKind,
        string ReturnTypeExpression,
        ImmutableArray<ActorParameterModel> Parameters,
        ImmutableArray<ActorParameterModel> PayloadParameters,
        string ArgsTypeName);

    [ExcludeFromCodeCoverage]
    private sealed record ActorParameterModel(
        string Name,
        string TypeName,
        int Position,
        bool IsCancellationToken,
        bool HasDefaultValue,
        object? DefaultValue);

    [ExcludeFromCodeCoverage]
    private sealed record ConstructorParameterModel(string Name, string TypeName, bool IsActorId);

    [ExcludeFromCodeCoverage]
    private sealed record UpcasterModel(string ImplementationType, INamedTypeSymbol From, INamedTypeSymbol To)
    {
        public string FromType => TypeName(From);

        public string ToType => TypeName(To);
    }

    [ExcludeFromCodeCoverage]
    private sealed record StateTypeModel(
        string TypeName,
        string MetadataName,
        string SimpleName,
        string CanonicalName,
        string Version,
        string ShapeHash,
        bool HasPublicParameterlessConstructor,
        ImmutableArray<StateMemberModel> Members);

    [ExcludeFromCodeCoverage]
    private sealed record StateMemberModel(string Name, string TypeName, string MetadataName, bool CanWrite, bool IsRequired);

    [ExcludeFromCodeCoverage]
    private sealed record StateNodeModel(int Index, StateTypeModel State)
    {
        public string MethodSuffix => SanitizeIdentifier(State.MetadataName) + "_" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    [ExcludeFromCodeCoverage]
    private sealed record StateEdgeModel(
        int FromIndex,
        int ToIndex,
        string FromType,
        string ToType,
        string? UpcasterType,
        bool IsGenerated,
        ImmutableArray<StateMemberModel> CopiedMembers);

    [ExcludeFromCodeCoverage]
    private sealed record StateFamilyModel(string CanonicalName, ImmutableArray<StateNodeModel> Nodes, ImmutableArray<StateEdgeModel> Edges)
    {
        public string MethodSuffix => SanitizeIdentifier(CanonicalName);
    }

    private enum MethodReturnKind
    {
        Unsupported,
        Task,
        TaskOfT,
        ValueTask,
        ValueTaskOfT,
    }

    [ExcludeFromCodeCoverage]
    private sealed class ActorInterfaceModelComparer : IEqualityComparer<ActorInterfaceModel>
    {
        public static readonly ActorInterfaceModelComparer Instance = new();
        public bool Equals(ActorInterfaceModel? x, ActorInterfaceModel? y) => StringComparer.Ordinal.Equals(x?.FullName, y?.FullName);
        public int GetHashCode(ActorInterfaceModel obj) => StringComparer.Ordinal.GetHashCode(obj.FullName);
    }

    [ExcludeFromCodeCoverage]
    private sealed class ActorModelComparer : IEqualityComparer<ActorModel>
    {
        public static readonly ActorModelComparer Instance = new();
        public bool Equals(ActorModel? x, ActorModel? y) => StringComparer.Ordinal.Equals(x?.ImplementationType, y?.ImplementationType);
        public int GetHashCode(ActorModel obj) => StringComparer.Ordinal.GetHashCode(obj.ImplementationType);
    }

    [ExcludeFromCodeCoverage]
    private sealed class UpcasterModelComparer : IEqualityComparer<UpcasterModel>
    {
        public static readonly UpcasterModelComparer Instance = new();
        public bool Equals(UpcasterModel? x, UpcasterModel? y) =>
            StringComparer.Ordinal.Equals(x?.ImplementationType, y?.ImplementationType)
            && StringComparer.Ordinal.Equals(x?.FromType, y?.FromType)
            && StringComparer.Ordinal.Equals(x?.ToType, y?.ToType);

        public int GetHashCode(UpcasterModel obj) =>
            StringComparer.Ordinal.GetHashCode(obj.ImplementationType)
            ^ StringComparer.Ordinal.GetHashCode(obj.FromType)
            ^ StringComparer.Ordinal.GetHashCode(obj.ToType);
    }

    [ExcludeFromCodeCoverage]
    private sealed class StateTypeModelComparer : IEqualityComparer<StateTypeModel>
    {
        public static readonly StateTypeModelComparer Instance = new();
        public bool Equals(StateTypeModel? x, StateTypeModel? y) => StringComparer.Ordinal.Equals(x?.TypeName, y?.TypeName);
        public int GetHashCode(StateTypeModel obj) => StringComparer.Ordinal.GetHashCode(obj.TypeName);
    }

    [ExcludeFromCodeCoverage]
    private sealed class StateFamilyModelComparer : IEqualityComparer<StateFamilyModel>
    {
        public static readonly StateFamilyModelComparer Instance = new();
        public bool Equals(StateFamilyModel? x, StateFamilyModel? y) => StringComparer.Ordinal.Equals(x?.CanonicalName, y?.CanonicalName);
        public int GetHashCode(StateFamilyModel obj) => StringComparer.Ordinal.GetHashCode(obj.CanonicalName);
    }

    [ExcludeFromCodeCoverage]
    private sealed class NumericVersionComparer : IComparer<string>
    {
        public static readonly NumericVersionComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var xOk = long.TryParse(x, out var xValue);
            var yOk = long.TryParse(y, out var yValue);
            if (xOk && yOk)
            {
                return xValue.CompareTo(yValue);
            }

            if (xOk)
            {
                return 1;
            }

            if (yOk)
            {
                return -1;
            }

            return StringComparer.Ordinal.Compare(x, y);
        }
    }
}
