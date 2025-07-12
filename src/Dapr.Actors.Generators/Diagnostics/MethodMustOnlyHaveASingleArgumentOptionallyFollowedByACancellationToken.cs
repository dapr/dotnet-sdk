using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators.Diagnostics;

internal static class MethodMustOnlyHaveASingleArgumentOptionallyFollowedByACancellationToken
{
    public const string DiagnosticId = "DAPR0002";
    public const string Title = "Invalid method signature";
    public const string MessageFormat = "Only methods with a single argument or a single argument followed by a cancellation token are supported";
    public const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static Diagnostic CreateDiagnostic(ISymbol symbol) => Diagnostic.Create(
        Rule,
        symbol.Locations.First(),
        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}