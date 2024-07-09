﻿// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using Dapr.Actors.Generators.Diagnostics;
using Dapr.Actors.Generators.Extensions;
using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators;

/// <summary>
/// Generates strongly-typed actor clients that use the non-remoting actor proxy.
/// </summary>
[Generator]
public sealed class ActorClientGenerator : IIncrementalGenerator
{
    private const string GeneratorsNamespace = "Dapr.Actors.Generators";

    private const string ActorMethodAttributeTypeName = "ActorMethodAttribute";
    private const string ActorMethodAttributeFullTypeName = GeneratorsNamespace + "." + ActorMethodAttributeTypeName;

    private const string GenerateActorClientAttribute = "GenerateActorClientAttribute";
    private const string GenerateActorClientAttributeFullTypeName = GeneratorsNamespace + "." + GenerateActorClientAttribute;

    private static string ActorMethodAttributeSourceText(string generatorNamespace)
    {
        if (generatorNamespace == null)
        {
            throw new ArgumentNullException(nameof(generatorNamespace));
        }

        var source = $@"
// <auto-generated/>

#nullable enable

using System;

namespace {generatorNamespace}
{{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class ActorMethodAttribute : Attribute
    {{
        public string? Name {{ get; set; }}
    }}
}}";

        return source;
    }
    private static string GenerateActorClientAttributeSourceText(string generatorNamespace)
    {
        if (generatorNamespace == null)
        {
            throw new ArgumentNullException(nameof(generatorNamespace));
        }

        string source = $@"
// <auto-generated/>

#nullable enable

using System;

namespace {generatorNamespace}
{{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class GenerateActorClientAttribute : Attribute
    {{
        public string? Name {{ get; set; }}

        public string? Namespace {{ get; set; }}
    }}
}}";

        return source;
    }

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context =>
        {
            context.AddSource($"{ActorMethodAttributeFullTypeName}.g.cs", ActorMethodAttributeSourceText(GeneratorsNamespace));
            context.AddSource($"{GenerateActorClientAttributeFullTypeName}.g.cs", GenerateActorClientAttributeSourceText(GeneratorsNamespace));
        });

        // Register the attribute that triggers the generation of actor clients.
        IncrementalValuesProvider<ActorClientDescriptor?> actorClientsToGenerate = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateActorClientAttributeFullTypeName,
                predicate: static (_, _) => true,
                transform: static (gasc, cancellationToken) => GetActorClientDescription(gasc, cancellationToken));

        context.RegisterSourceOutput(actorClientsToGenerate, GenerateActorClientCode);
    }

    /// <summary>
    /// Returns the descriptor for the actor client to generate.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    static ActorClientDescriptor? GetActorClientDescription(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        // Return the attribute data of GenerateActorClientAttribute, which is the attribute that triggered this generator
        // and is expected to be the only attribute in the list of matching attributes.
        var attributeData = context.Attributes.Single();

        var actorInterfaceSymbol = (INamedTypeSymbol)context.TargetSymbol;

        // Use the namespace specified in the attribute, or the namespace of the actor interface if not specified.
        var namespaceName = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Namespace").Value.Value?.ToString()
            ?? actorInterfaceSymbol.ContainingNamespace.ToDisplayString();

        // Use the name specified in the attribute, or the name of the actor interface with a "Client" suffix if not specified.
        var clientName = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString()
            ?? $"{(actorInterfaceSymbol.Name.StartsWith("I") ? actorInterfaceSymbol.Name.Substring(1) : actorInterfaceSymbol.Name)}Client";

        var members = actorInterfaceSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .ToList();

        return new ActorClientDescriptor
        {
            NamespaceName = namespaceName,
            ClientTypeName = clientName,
            Methods = members,
            Accessibility = actorInterfaceSymbol.DeclaredAccessibility,
            InterfaceType = actorInterfaceSymbol,
            Compilation = context.SemanticModel.Compilation,
        };
    }

    static void GenerateActorClientCode(SourceProductionContext context, ActorClientDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return;
        }

        try
        {
            var actorMethodAttributeSymbol = descriptor.Compilation.GetTypeByMetadataName(ActorMethodAttributeFullTypeName)
                ?? throw new InvalidOperationException("Could not find ActorMethodAttribute.");

            var cancellationTokenSymbol = descriptor.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken")
                ?? throw new InvalidOperationException("Could not find CancellationToken.");

            var methodImplementations = string.Join(
                "\n",
                descriptor.Methods.Select(member => GenerateMethodImplementation(context, member, actorMethodAttributeSymbol, cancellationTokenSymbol)));

            var source = $@"
// <auto-generated/>

namespace {descriptor.NamespaceName}
{{
    {GetTextAccessibility(descriptor.Accessibility)} sealed class {descriptor.ClientTypeName} : {descriptor.InterfaceType.ToString()}
    {{
        private readonly Dapr.Actors.Client.ActorProxy actorProxy;

        public {descriptor.ClientTypeName}(Dapr.Actors.Client.ActorProxy actorProxy)
        {{
            this.actorProxy = actorProxy;
        }}

        {methodImplementations}
    }}
}}";

            context.AddSource($"{descriptor.FullyQualifiedTypeName}.g.cs", source);
        }
        catch (DiagnosticsException e)
        {
            foreach (var diagnostic in e.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    /// Describes an actor client to generate.
    /// </summary>
    private class ActorClientDescriptor
    {
        /// <summary>
        /// Gets the actor interface symbol.
        /// </summary>
        public INamedTypeSymbol InterfaceType { get; set; } = null!;

        /// <summary>
        /// Accessibility of the generated client.
        /// </summary>
        public Accessibility Accessibility { get; set; }

        /// <summary>
        /// Namespace of the generated client.
        /// </summary>
        public string NamespaceName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the generated client.
        /// </summary>
        public string ClientTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Fully qualified type name of the generated client.
        /// </summary>
        public string FullyQualifiedTypeName => $"{NamespaceName}.{ClientTypeName}";

        /// <summary>
        /// Methods to generate in the client.
        /// </summary>
        public IEnumerable<IMethodSymbol> Methods { get; set; } = Array.Empty<IMethodSymbol>();

        /// <summary>
        /// Compilation to use for generating the client.
        /// </summary>
        public Compilation Compilation { get; set; } = null!;
    }

    // TODO: check there is a better way to get the accessibility
    private static string GetTextAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "protected internal",
            _ => throw new InvalidOperationException("Unexpected accessibility.")
        };
    }

    private static string GenerateMethodImplementation(
        SourceProductionContext context,
        IMethodSymbol method,
        INamedTypeSymbol generateActorClientAttributeSymbol,
        INamedTypeSymbol cancellationTokenSymbol)
    {
        int cancellationTokenIndex = method.Parameters.IndexOf(p => p.Type.Equals(cancellationTokenSymbol, SymbolEqualityComparer.Default));
        var cancellationTokenParameter = cancellationTokenIndex != -1 ? method.Parameters[cancellationTokenIndex] : null;

        if (cancellationTokenParameter is not null && cancellationTokenIndex != method.Parameters.Length - 1)
        {
            context.ReportDiagnostic(CancellationTokensMustBeTheLastArgument.CreateDiagnostic(cancellationTokenParameter));
        }

        if ((method.Parameters.Length > 1 && cancellationTokenIndex == -1) || (method.Parameters.Length > 2))
        {
            context.ReportDiagnostic(MethodMustOnlyHaveASingleArgumentOptionallyFollowedByACancellationToken.CreateDiagnostic(method));
        }

        var attributeData = method.GetAttributes()
            .SingleOrDefault(a => a.AttributeClass?.Equals(generateActorClientAttributeSymbol, SymbolEqualityComparer.Default) == true);

        string? actualMethodName = attributeData?.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString() ?? method.Name;

        var requestParameter = method.Parameters.Length > 0 && cancellationTokenIndex != 0 ? method.Parameters[0] : null;

        var returnTypeArgument = (method.ReturnType as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();

        string argumentDefinitions = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));

        if (cancellationTokenParameter is not null
            && cancellationTokenParameter.IsOptional
            && cancellationTokenParameter.HasExplicitDefaultValue
            && cancellationTokenParameter.ExplicitDefaultValue is null)
        {
            argumentDefinitions = argumentDefinitions + " = default";
        }

        string argumentList = string.Join(", ", new[] { $@"""{actualMethodName}""" }.Concat(method.Parameters.Select(p => p.Name)));

        string templateArgs =
            returnTypeArgument is not null
                ? $"<{(requestParameter is not null ? $"{requestParameter.Type}, " : "")}{returnTypeArgument}>"
                : "";

        return
        $@"public {method.ReturnType} {method.Name}({argumentDefinitions})
        {{
            return this.actorProxy.InvokeMethodAsync{templateArgs}({argumentList});
        }}";
    }
}

internal sealed class DiagnosticsException : Exception
{
    public DiagnosticsException(IEnumerable<Diagnostic> diagnostics)
        : base(string.Join("\n", diagnostics.Select(d => d.ToString())))
    {
        this.Diagnostics = diagnostics.ToArray();
    }

    public IEnumerable<Diagnostic> Diagnostics { get; }
}
