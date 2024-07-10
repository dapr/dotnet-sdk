using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators
{
    internal sealed class DiagnosticsException : Exception
    {
        public DiagnosticsException(IEnumerable<Diagnostic> diagnostics)
            : base(string.Join("\n", diagnostics.Select(d => d.ToString())))
        {
            this.Diagnostics = diagnostics.ToArray();
        }

        public IEnumerable<Diagnostic> Diagnostics { get; }
    }
}
