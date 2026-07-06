using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators;

/// <summary>
/// Exception thrown when diagnostics are encountered during code generation.
/// </summary>
internal sealed class DiagnosticsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsException"/> class.
    /// </summary>
    /// <param name="diagnostics">List of diagnostics generated.</param>
    public DiagnosticsException(IEnumerable<Diagnostic> diagnostics)
        : base(string.Join("\n", diagnostics.Select(d => d.ToString())))
    {
        this.Diagnostics = diagnostics.ToArray();
    }

    /// <summary>
    /// Diagnostics encountered during code generation.
    /// </summary>
    public ICollection<Diagnostic> Diagnostics { get; }
}