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
        IncrementalValuesProvider<ActorClientDescriptor> actorClientsToGenerate = context.SyntaxProvider
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
    /// <param name="context">Current generator syntax context passed from generator pipeline.</param>
    /// <param name="cancellationToken">Cancellation token used to interrupt the generation.</param>
    /// <returns>Returns the descriptor of actor client to generate.</returns>
    private static ActorClientDescriptor CreateActorClientDescriptor(
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
    /// <param name="context">Context passed from the source generator when it has registered an output.</param>
    /// <param name="descriptor">Descriptor of actor client to generate.</param>
    /// <exception cref="InvalidOperationException">Throws when one or more required symbols assembly are missing.</exception>
    private static void GenerateActorClientCode(SourceProductionContext context, ActorClientDescriptor descriptor)
    {
        try
        {
            var actorMethodAttributeSymbol = descriptor.Compilation.GetTypeByMetadataName(Constants.ActorMethodAttributeFullTypeName)
                ?? throw new InvalidOperationException("Could not find ActorMethodAttribute type.");

            var cancellationTokenSymbol = descriptor.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken")
                ?? throw new InvalidOperationException("Could not find CancellationToken type.");

            var actorClientBaseInterface = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(descriptor.InterfaceType.ToString()));
            var autoGeneratedComment = SyntaxFactory.Comment("// <auto-generated/>");
            var nullableAnnotation = SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true));
            var actorProxyTypeSyntax = SyntaxFactory.ParseTypeName(Constants.ActorProxyTypeName);

            // Generate the actor proxy field to store the actor proxy instance.
            var actorProxyFieldDeclaration = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(actorProxyTypeSyntax)
                .WithVariables(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("actorProxy")))))
                .WithModifiers(SyntaxFactory.TokenList(new[]
                {
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)
                }));

            // Generate the constructor for the actor client.
            var actorCtor = SyntaxFactory.ConstructorDeclaration(SyntaxFactory.Identifier(descriptor.ClientTypeName))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("actorProxy")).WithType(actorProxyTypeSyntax)
                })))
                .WithBody(SyntaxFactory.Block(SyntaxFactory.List(new StatementSyntax[]
                {
                    SyntaxFactoryHelpers.ThrowIfArgumentNull("actorProxy"),
                    SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                         SyntaxFactory.MemberAccessExpression(
                             SyntaxKind.SimpleMemberAccessExpression,
                             SyntaxFactory.ThisExpression(),
                             SyntaxFactory.IdentifierName("actorProxy")),
                        SyntaxFactory.IdentifierName("actorProxy"))
                    ),
                })));

            var actorMethods = descriptor.Methods
                .OrderBy(member => member.DeclaredAccessibility)
                .ThenBy(member => member.Name)
                .Select(member => GenerateMethodImplementation(member, actorMethodAttributeSymbol, cancellationTokenSymbol));

            var actorMembers = new List<MemberDeclarationSyntax>()
                .Append(actorProxyFieldDeclaration)
                .Append(actorCtor)
                .Concat(actorMethods);

            var actorClientClassModifiers = new List<SyntaxKind>()
                .Concat(SyntaxFactoryHelpers.GetSyntaxKinds(descriptor.Accessibility))
                .Append(SyntaxKind.SealedKeyword)
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
    /// Generates the method implementation for the specified method.
    /// </summary>
    /// <param name="method">
    /// MethodSymbol extracted from the actor interface representing the method to generate.
    /// </param>
    /// <param name="generateActorClientAttributeSymbol">
    /// ActorMethodAttribute symbol used to extract the original actor method name to use when making runtime calls.
    /// </param>
    /// <param name="cancellationTokenSymbol">Symbol used to search the position of cancellationToken between method parameters.</param>
    /// <returns>Returns a <see cref="MethodDeclarationSyntax"/> of the generated method.</returns>
    private static MethodDeclarationSyntax GenerateMethodImplementation(
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

        // If there are any diagnostics, throw an exception to report them and stop the generation.
        if (diagnostics.Any())
        {
            throw new DiagnosticsException(diagnostics);
        }

        // Get the ActorMethodAttribute data for the method, if it exists.
        var attributeData = method.GetAttributes()
            .SingleOrDefault(a => a.AttributeClass?.Equals(generateActorClientAttributeSymbol, SymbolEqualityComparer.Default) == true);

        // Generate the method name to use for the Dapr actor method invocation, using the Name property of ActorMethodAttribute if specified,
        // or the original method name otherwise.
        var daprMethodName = attributeData?.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString() ?? method.Name;

        var methodModifiers = new List<SyntaxKind>()
            .Concat(SyntaxFactoryHelpers.GetSyntaxKinds(method.DeclaredAccessibility))
            .Select(sk => SyntaxFactory.Token(sk));

        // Define the parameters to pass to the actor proxy method invocation.
        // Exclude the CancellationToken parameter if it exists, because it need to be handled separately.
        var methodParameters = method.Parameters
            .Where(p => p.Type is not INamedTypeSymbol { Name: "CancellationToken" })
            .Select(p => SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name)).WithType(SyntaxFactory.ParseTypeName(p.Type.ToString())));

        // Append the CancellationToken parameter if it exists, handling the case where it is optional and has no default value.
        if (cancellationTokenParameter is not null)
        {
            if (cancellationTokenParameter.IsOptional
                && cancellationTokenParameter.HasExplicitDefaultValue
                && cancellationTokenParameter.ExplicitDefaultValue is null)
            {
                methodParameters = methodParameters.Append(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier(cancellationTokenParameter.Name))
                        .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)))
                        .WithType(SyntaxFactory.ParseTypeName(cancellationTokenParameter.Type.ToString())));
            }
            else
            {
                methodParameters = methodParameters.Append(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier(cancellationTokenParameter.Name))
                        .WithType(SyntaxFactory.ParseTypeName(cancellationTokenParameter.Type.ToString())));
            }
        }

        // Extract the return type of the original method.
        var methodReturnType = (INamedTypeSymbol)method.ReturnType;

        // Generate the method implementation.
        var generatedMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(method.ReturnType.ToString()), method.Name)
            .WithModifiers(SyntaxFactory.TokenList(methodModifiers))
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(methodParameters)))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.List(new StatementSyntax[]
            {
                SyntaxFactory.ReturnStatement(SyntaxFactoryHelpers.ActorProxyInvokeMethodAsync(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        SyntaxFactory.IdentifierName("actorProxy")),
                    daprMethodName,
                    method.Parameters,
                    methodReturnType.TypeArguments
                )),
            })));

        return generatedMethod;
    }
}
