using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Dapr.Pubsub.Analyzers.Test;

internal class VerifyCodeFix
{
    public static async Task RunTest<T>(string code, string expectedChangedCode) where T : CodeFixProvider, new()
    {
        var (diagnostics, document, workspace) = await Utilities.GetDiagnosticsAdvanced(code);

        Assert.Single(diagnostics);

        var diagnostic = diagnostics[0];

        var codeFixProvider = new T();

        CodeAction? registeredCodeAction = null;

        var context = new CodeFixContext(document, diagnostic, (codeAction, _) =>
        {
            if (registeredCodeAction != null)
                throw new Exception("Code action was registered more than once");

            registeredCodeAction = codeAction;

        }, CancellationToken.None);

        await codeFixProvider.RegisterCodeFixesAsync(context);

        if (registeredCodeAction == null)
            throw new Exception("Code action was not registered");

        var operations = await registeredCodeAction.GetOperationsAsync(CancellationToken.None);

        foreach (var operation in operations)
        {
            operation.Apply(workspace, CancellationToken.None);
        }

        var updatedDocument = workspace.CurrentSolution.GetDocument(document.Id) ?? throw new Exception("Updated document is null");
        var newCode = (await updatedDocument.GetTextAsync()).ToString();

        // Normalize whitespace
        string NormalizeWhitespace(string input)
        {
            var separator = new[] { ' ', '\r', '\n' };
            return string.Join(" ", input.Split(separator, StringSplitOptions.RemoveEmptyEntries));
        }

        var normalizedExpectedCode = NormalizeWhitespace(expectedChangedCode);
        var normalizedNewCode = NormalizeWhitespace(newCode);

        Assert.Equal(normalizedExpectedCode, normalizedNewCode);
    }
}
