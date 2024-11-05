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
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.Actors.Generators;

/// <summary>
/// Generates an extension method that can be used during dependency injection to register all actor types.
/// </summary>
[Generator]
public sealed class ActorRegistrationGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the generator and registers the syntax receiver.
    /// </summary>
    /// <param name="context">The <see cref="T:Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext" /> to register callbacks on</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsClassDeclaration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());
        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Right, spc));
    }

    private static bool IsClassDeclaration(SyntaxNode node) => node is ClassDeclarationSyntax;

    private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        if (model.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        var actorClass = context.SemanticModel.Compilation.GetTypeByMetadataName("Dapr.Actors.Runtime.Actor");
        return classSymbol.BaseType != null && classSymbol.BaseType.Equals(actorClass, SymbolEqualityComparer.Default) ? classSymbol : null;
    }

    private static void Execute(ImmutableArray<INamedTypeSymbol?> actorTypes,
        SourceProductionContext context)
    {
        var validActorTypes = actorTypes.Where(static t => t is not null).Cast<INamedTypeSymbol>().ToList();
        var source = GenerateActorRegistrationSource(validActorTypes);
        context.AddSource("ActorRegistrationExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Generates the source code for the actor registration method.
    /// </summary>
    /// <param name="actorTypes">The list of actor types to register.</param>
    /// <returns>The generated source code as a string.</returns>
    private static string GenerateActorRegistrationSource(IReadOnlyList<INamedTypeSymbol> actorTypes)
    {
#pragma warning disable RS1035
        var registrations = string.Join(Environment.NewLine,
#pragma warning restore RS1035
            actorTypes.Select(t => $"options.Actors.RegisterActor<{t.ToDisplayString()}>();"));

        return $@"
using Microsoft.Extensions.DependencyInjection;
using Dapr.Actors.Runtime;

/// <summary>
/// Extension methods for registering Dapr actors.
/// </summary>
public static class ActorRegistrationExtensions
{{
    /// <summary>
    /// Registers all discovered actor types with the Dapr actor runtime.
    /// </summary>
    public static void RegisterAllActors(this IServiceCollection services)
    {{
        services.AddActors(options => 
        {{
            {registrations}
        }});
    }}
}}";
    }
}
