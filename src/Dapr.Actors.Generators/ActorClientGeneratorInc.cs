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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Actors.Generators;

/// <summary>
/// Generates strongly-typed actor clients that use the non-remoting actor proxy.
/// </summary>
[Generator]
public sealed class ActorClientGeneratorInc : IIncrementalGenerator
{
    private const string GeneratorsNamespace = "Dapr.Actors.Generators";

    private const string ActorMethodAttributeTypeName = "ActorMethodAttribute";
    private const string ActorMethodAttributeFullTypeName = GeneratorsNamespace + "." + ActorMethodAttributeTypeName;

    private const string GenerateActorClientAttribute = "GenerateActorClientAttribute";
    private const string GenerateActorClientAttributeFullTypeName = GeneratorsNamespace + "." + GenerateActorClientAttribute;

    private const string ActorMethodAttributeText = $@"
        // <auto-generated/>

        #nullable enable

        using System;

        namespace {GeneratorsNamespace}
        {{
            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            internal sealed class ActorMethodAttribute : Attribute
            {{
                public string? Name {{ get; set; }}
            }}
        }}";

    private const string GenerateActorClientAttributeText = $@"
        // <auto-generated/>

        #nullable enable

        using System;

        namespace {GeneratorsNamespace}
        {{
            [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
            internal sealed class GenerateActorClientAttribute : Attribute
            {{
                public string? Name {{ get; set; }}

                public string? Namespace {{ get; set; }}
            }}
        }}";

    private sealed class ActorInterfaceSyntaxReceiver : ISyntaxContextReceiver
    {
        private readonly List<INamedTypeSymbol> models = new();

        public IEnumerable<INamedTypeSymbol> Models => this.models;

        #region ISyntaxContextReceiver Members

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is not InterfaceDeclarationSyntax interfaceDeclarationSyntax
                || interfaceDeclarationSyntax.AttributeLists.Count == 0)
            {
                return;
            }

            var interfaceSymbol = context.SemanticModel.GetDeclaredSymbol(interfaceDeclarationSyntax) as INamedTypeSymbol;

            if (interfaceSymbol is null
                || !interfaceSymbol.GetAttributes().Any(a => a.AttributeClass?.ToString() == GenerateActorClientAttributeFullTypeName))
            {
                return;
            }

            this.models.Add(interfaceSymbol);
        }

        #endregion
    }

    #region ISourceGenerator Members

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context =>
        {
            context.AddSource($"{ActorMethodAttributeFullTypeName}.g.cs", ActorMethodAttributeText);
            context.AddSource($"{GenerateActorClientAttributeFullTypeName}.g.cs", GenerateActorClientAttributeText);
        });
    }

    /// <inheritdoc />
    [Obsolete]
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not ActorInterfaceSyntaxReceiver actorInterfaceSyntaxReceiver)
        {
            return;
        }

        var actorMethodAttributeSymbol = context.Compilation.GetTypeByMetadataName(ActorMethodAttributeFullTypeName) ?? throw new InvalidOperationException("Could not find ActorMethodAttribute.");
        var generateActorClientAttributeSymbol = context.Compilation.GetTypeByMetadataName(GenerateActorClientAttributeFullTypeName) ?? throw new InvalidOperationException("Could not find GenerateActorClientAttribute.");
        var cancellationTokenSymbol = context.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken") ?? throw new InvalidOperationException("Could not find CancellationToken.");

        foreach (var interfaceSymbol in actorInterfaceSyntaxReceiver.Models)
        {
            try
            {
                var actorInterfaceTypeName = interfaceSymbol.Name;
                var fullyQualifiedActorInterfaceTypeName = interfaceSymbol.ToString();

                var attributeData = interfaceSymbol.GetAttributes().Single(a => a.AttributeClass?.Equals(generateActorClientAttributeSymbol, SymbolEqualityComparer.Default) == true);

                var accessibility = GetClientAccessibility(interfaceSymbol);
                var clientTypeName = GetClientName(interfaceSymbol, attributeData);
                var namespaceName = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Namespace").Value.Value?.ToString() ?? interfaceSymbol.ContainingNamespace.ToDisplayString();

                var members = interfaceSymbol.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToList();

                var methodImplementations = String.Join("\n", members.Select(member => GenerateMethodImplementation(member, actorMethodAttributeSymbol, cancellationTokenSymbol)));

                var source = $@"
// <auto-generated/>

namespace {namespaceName}
{{
    {accessibility} sealed class {clientTypeName} : {fullyQualifiedActorInterfaceTypeName}
    {{
        private readonly Dapr.Actors.Client.ActorProxy actorProxy;

        public {clientTypeName}(Dapr.Actors.Client.ActorProxy actorProxy)
        {{
            this.actorProxy = actorProxy;
        }}

        {methodImplementations}
    }}
}}
";
                // Add the source code to the compilation
                context.AddSource($"{namespaceName}.{clientTypeName}.g.cs", source);
            }
            catch (DiagnosticsException e)
            {
                foreach (var diagnostic in e.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    /// <inheritdoc />
    [Obsolete]
    public void Initialize(GeneratorInitializationContext context)
    {
        /*
        while (!Debugger.IsAttached)
        {
            System.Threading.Thread.Sleep(500);
        }
        */

        context.RegisterForPostInitialization(
            i =>
            {
                i.AddSource($"{ActorMethodAttributeFullTypeName}.g.cs", ActorMethodAttributeText);
                i.AddSource($"{GenerateActorClientAttributeFullTypeName}.g.cs", GenerateActorClientAttributeText);
            });

        context.RegisterForSyntaxNotifications(() => new ActorInterfaceSyntaxReceiver());
    }

    #endregion

    private static string GetClientAccessibility(INamedTypeSymbol interfaceSymbol)
    {
        return interfaceSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "protected internal",
            _ => throw new InvalidOperationException("Unexpected accessibility.")
        };
    }

    private static string GetClientName(INamedTypeSymbol interfaceSymbol, AttributeData attributeData)
    {
        string? clientName = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString();

        clientName ??= $"{(interfaceSymbol.Name.StartsWith("I") ? interfaceSymbol.Name.Substring(1) : interfaceSymbol.Name)}Client";

        return clientName;
    }

    private static string GenerateMethodImplementation(IMethodSymbol method, INamedTypeSymbol generateActorClientAttributeSymbol, INamedTypeSymbol cancellationTokenSymbol)
    {
        int cancellationTokenIndex = method.Parameters.IndexOf(p => p.Type.Equals(cancellationTokenSymbol, SymbolEqualityComparer.Default));
        var cancellationTokenParameter = cancellationTokenIndex != -1 ? method.Parameters[cancellationTokenIndex] : null;

        if (cancellationTokenParameter is not null && cancellationTokenIndex != method.Parameters.Length - 1)
        {
            throw new DiagnosticsException(new[]
            {
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPR0001",
                        "Invalid method signature.",
                        "Cancellation tokens must be the last argument.",
                        "Dapr.Actors.Generators",
                        DiagnosticSeverity.Error,
                        true),
                    cancellationTokenParameter.Locations.First())
            });
        }

        if ((method.Parameters.Length > 1 && cancellationTokenIndex == -1)
            || (method.Parameters.Length > 2))
        {
            throw new DiagnosticsException(new[]
            {
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DAPR0002",
                        "Invalid method signature.",
                        "Only methods with a single argument or a single argument followed by a cancellation token are supported.",
                        "Dapr.Actors.Generators",
                        DiagnosticSeverity.Error,
                        true),
                    method.Locations.First())
            });
        }

        var attributeData = method.GetAttributes().SingleOrDefault(a => a.AttributeClass?.Equals(generateActorClientAttributeSymbol, SymbolEqualityComparer.Default) == true);

        string? actualMethodName = attributeData?.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString() ?? method.Name;

        var requestParameter = method.Parameters.Length > 0 && cancellationTokenIndex != 0 ? method.Parameters[0] : null;

        var returnTypeArgument = (method.ReturnType as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();

        string argumentDefinitions = String.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));

        if (cancellationTokenParameter is not null
            && cancellationTokenParameter.IsOptional
            && cancellationTokenParameter.HasExplicitDefaultValue
            && cancellationTokenParameter.ExplicitDefaultValue is null)
        {
            argumentDefinitions = argumentDefinitions + " = default";
        }

        string argumentList = String.Join(", ", new[] { $@"""{actualMethodName}""" }.Concat(method.Parameters.Select(p => p.Name)));

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

internal static class Extensions
{
    public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        int index = 0;

        foreach (var item in source)
        {
            if (predicate(item))
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}

internal sealed class DiagnosticsException : Exception
{
    public DiagnosticsException(IEnumerable<Diagnostic> diagnostics)
        : base(String.Join("\n", diagnostics.Select(d => d.ToString())))
    {
        this.Diagnostics = diagnostics.ToArray();
    }

    public IEnumerable<Diagnostic> Diagnostics { get; }
}
