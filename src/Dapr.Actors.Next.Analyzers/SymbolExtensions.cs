using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Next.Analyzers;

internal static class SymbolExtensions
{
    internal static readonly SymbolDisplayFormat BaselineFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    internal static bool InheritsFromOrEquals(this ITypeSymbol? symbol, string metadataName)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool Implements(this ITypeSymbol symbol, string metadataName)
    {
        foreach (var implemented in symbol.AllInterfaces)
        {
            if (implemented.ToDisplayString() == metadataName ||
                implemented.OriginalDefinition.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasAttribute(this ISymbol symbol, string metadataName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsActorImplementation(this INamedTypeSymbol type) =>
        type.InheritsFromOrEquals("Dapr.Actors.Next.Abstractions.Actor") ||
        type.HasAttribute("Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute");

    internal static bool IsActorInterface(this INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Interface &&
        (type.Implements("Dapr.Actors.Next.Abstractions.IActor") ||
         type.HasAttribute("Dapr.Actors.Next.Abstractions.Attributes.GenerateActorClientAttribute"));

    internal static bool IsSupportedActorReturnType(this ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var original = named.OriginalDefinition.ToDisplayString();
        return original is "System.Threading.Tasks.Task" or
            "System.Threading.Tasks.Task<TResult>" or
            "System.Threading.Tasks.ValueTask" or
            "System.Threading.Tasks.ValueTask<TResult>" or
            "System.Collections.Generic.IAsyncEnumerable<T>";
    }

    internal static string BaselineName(this ITypeSymbol type) => type.ToDisplayString(BaselineFormat);

    internal static string BaselineName(this ISymbol symbol) => symbol.ToDisplayString(BaselineFormat);
}
