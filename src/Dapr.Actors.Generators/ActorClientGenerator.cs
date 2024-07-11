// ------------------------------------------------------------------------
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

using System.Collections.Immutable;
using Dapr.Actors.Generators.Diagnostics;
using Dapr.Actors.Generators.Extensions;
using Dapr.Actors.Generators.Helpers;
using Dapr.Actors.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Actors.Generators;

/// <summary>
/// Generates strongly-typed actor clients that use the non-remoting actor proxy.
/// </summary>
[Generator]
public sealed class ActorClientGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the source output that generates the attribute definitions for ActorMethodAttribute and GenerateActorClientAttribute.
        context.RegisterPostInitializationOutput(context =>
        {
            context.AddSource(
                $"{Constants.ActorMethodAttributeFullTypeName}.g.cs",
                Templates.ActorMethodAttributeSourceText(Constants.GeneratorsNamespace));

            context.AddSource(
                $"{Constants.GenerateActorClientAttributeFullTypeName}.g.cs",
                Templates.GenerateActorClientAttributeSourceText(Constants.GeneratorsNamespace));
        });

        // Register the value provider that triggers the generation of actor clients when detecting the GenerateActorClientAttribute.
        IncrementalValuesProvider<ActorClientDescriptor?> actorClientsToGenerate = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Constants.GenerateActorClientAttributeFullTypeName,
                predicate: static (_, _) => true,
                transform: static (gasc, cancellationToken) => CreateActorClientDescriptor(gasc, cancellationToken));

        // Register the source output that generates the actor clients.
        context.RegisterSourceOutput(actorClientsToGenerate, GenerateActorClientCode);
    }

    /// <summary>
    /// Returns the descriptor for the actor client to generate.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    static ActorClientDescriptor? CreateActorClientDescriptor(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        // Return the attribute data of GenerateActorClientAttribute, which is the attribute that triggered this generator
        // and is expected to be the only attribute in the list of matching attributes.
        var attributeData = context.Attributes.Single();

        var actorInterfaceSymbol = (INamedTypeSymbol)context.TargetSymbol;

        // Use the namespace specified in the GenerateActorClientAttribute, or the namespace of the actor interface if not specified.
        var namespaceName = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Namespace").Value.Value?.ToString()
            ?? actorInterfaceSymbol.ContainingNamespace.ToDisplayString();

        // Use the name specified in the GenerateActorClientAttribute, or the name of the actor interface with a "Client" suffix if not specified.
        var clientName = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString()
            ?? $"{(actorInterfaceSymbol.Name.StartsWith("I") ? actorInterfaceSymbol.Name.Substring(1) : actorInterfaceSymbol.Name)}Client";

        // Actor member to generate the client for.
        var members = actorInterfaceSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .ToImmutableArray();

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

    /// <summary>
    /// Generates the actor client code based on the specified descriptor.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="descriptor"></param>
    /// <exception cref="InvalidOperationException"></exception>
    static void GenerateActorClientCode(SourceProductionContext context, ActorClientDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return;
        }

        try
        {
            var actorMethodAttributeSymbol = descriptor.Compilation.GetTypeByMetadataName(Constants.ActorMethodAttributeFullTypeName)
                ?? throw new InvalidOperationException("Could not find ActorMethodAttribute type.");

            var cancellationTokenSymbol = descriptor.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken")
                ?? throw new InvalidOperationException("Could not find CancellationToken type.");

            var actorClientBaseInterface = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(descriptor.InterfaceType.ToString()));
            var autoGeneratedComment = SyntaxFactory.Comment("// <auto-generated/>");
            var nullableAnnotation = SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true));

            var actorProxyField = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("Dapr.Actors.Client.ActorProxy"))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("actorProxy")))))
                .WithModifiers(SyntaxFactory.TokenList(new[]
                {
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)
                }));

            var actorCtor = SyntaxFactory.ConstructorDeclaration(SyntaxFactory.Identifier(descriptor.ClientTypeName))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("actorProxy")).WithType(SyntaxFactory.ParseTypeName("Dapr.Actors.Client.ActorProxy"))
                })))
                .WithBody(SyntaxFactory.Block(SyntaxFactory.List(new StatementSyntax[]
                {
                    SyntaxFactoryHelpers.ThrowIfArgumentNull("actorProxy"),
                    SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName("this.actorProxy"),
                        SyntaxFactory.IdentifierName("actorProxy"))
                    ),
                })));

            var actorMethods = descriptor.Methods
                .OrderBy(member => member.DeclaredAccessibility)
                .ThenBy(member => member.Name)
                .Select(member => GenerateMethodImplementation(member, actorMethodAttributeSymbol, cancellationTokenSymbol))
                .Select(m => SyntaxFactory.ParseMemberDeclaration(m)!);

            var actorMembers = new List<MemberDeclarationSyntax>()
                .Concat(actorProxyField)
                .Concat(actorCtor)
                .Concat(actorMethods)
                .ToList();

            var actorClientClassModifiers = new List<SyntaxKind>()
                .Concat(GetSyntaxKinds(descriptor.Accessibility))
                .Concat(SyntaxKind.SealedKeyword)
                .Select(sk => SyntaxFactory.Token(sk));

            var actorClientClassDeclaration = SyntaxFactory.ClassDeclaration(descriptor.ClientTypeName)
                .WithModifiers(SyntaxFactory.TokenList(actorClientClassModifiers))
                .WithMembers(SyntaxFactory.List(actorMembers))
                .WithBaseList(SyntaxFactory.BaseList(
                    SyntaxFactory.Token(SyntaxKind.ColonToken),
                    SyntaxFactory.SeparatedList<BaseTypeSyntax>(new[] { actorClientBaseInterface })));

            var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(descriptor.NamespaceName))
                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(new[] { actorClientClassDeclaration }))
                .WithLeadingTrivia(SyntaxFactory.TriviaList(new[] {
                    autoGeneratedComment,
                    nullableAnnotation,
                }));

            var compilationOutput = SyntaxFactory.CompilationUnit()
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(namespaceDeclaration))
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource($"{descriptor.FullyQualifiedTypeName}.g.cs", compilationOutput);
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
    /// Returns the syntax kinds for the specified accessibility.
    /// </summary>
    /// <param name="accessibility"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static IEnumerable<SyntaxKind> GetSyntaxKinds(Accessibility accessibility)
    {
        var syntaxKinds = new List<SyntaxKind>();

        switch (accessibility)
        {
            case Accessibility.Public:
                syntaxKinds.Add(SyntaxKind.PublicKeyword);
                break;
            case Accessibility.Internal:
                syntaxKinds.Add(SyntaxKind.InternalKeyword);
                break;
            case Accessibility.Private:
                syntaxKinds.Add(SyntaxKind.PrivateKeyword);
                break;
            case Accessibility.Protected:
                syntaxKinds.Add(SyntaxKind.ProtectedKeyword);
                break;
            case Accessibility.ProtectedAndInternal:
                syntaxKinds.Add(SyntaxKind.ProtectedKeyword);
                syntaxKinds.Add(SyntaxKind.InternalKeyword);
                break;
            default:
                throw new InvalidOperationException("Unexpected accessibility");
        }

        return syntaxKinds;
    }

    /// <summary>
    /// Generates the method implementation for the specified method.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="generateActorClientAttributeSymbol"></param>
    /// <param name="cancellationTokenSymbol"></param>
    /// <returns></returns>
    private static string GenerateMethodImplementation(
        IMethodSymbol method,
        INamedTypeSymbol generateActorClientAttributeSymbol,
        INamedTypeSymbol cancellationTokenSymbol)
    {
        int cancellationTokenIndex = method.Parameters.IndexOf(p => p.Type.Equals(cancellationTokenSymbol, SymbolEqualityComparer.Default));
        var cancellationTokenParameter = cancellationTokenIndex != -1 ? method.Parameters[cancellationTokenIndex] : null;
        var diagnostics = new List<Diagnostic>();

        if (cancellationTokenParameter is not null && cancellationTokenIndex != method.Parameters.Length - 1)
        {
            diagnostics.Add(CancellationTokensMustBeTheLastArgument.CreateDiagnostic(cancellationTokenParameter));
        }

        if ((method.Parameters.Length > 1 && cancellationTokenIndex == -1) || (method.Parameters.Length > 2))
        {
            diagnostics.Add(MethodMustOnlyHaveASingleArgumentOptionallyFollowedByACancellationToken.CreateDiagnostic(method));
        }

        if (diagnostics.Any())
        {
            throw new DiagnosticsException(diagnostics);
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
