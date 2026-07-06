using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators.Diagnostics;

internal static class CancellationTokensMustBeTheLastArgument
{
    public const string DiagnosticId = "DAPR0001";
    public const string Title = "Invalid method signature";
    public const string MessageFormat = "Cancellation tokens must be the last argument";
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