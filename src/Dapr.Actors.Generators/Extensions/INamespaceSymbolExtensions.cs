using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators.Extensions;

internal static class INamespaceSymbolExtensions
{
    /// <summary>
    /// Recursively gets all the types in a namespace.
    /// </summary>
    /// <param name="namespaceSymbol">The namespace symbol to search.</param>
    /// <returns>A collection of the named type symbols.</returns>
    public static IEnumerable<INamedTypeSymbol> GetNamespaceTypes(this INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol nestedNamespace:
                {
                    foreach (var nestedType in nestedNamespace.GetNamespaceTypes())
                    {
                        yield return nestedType;
                    }

                    break;
                }
                case INamedTypeSymbol namedType:
                    yield return namedType;
                    break;
            }
        }
    }
}
