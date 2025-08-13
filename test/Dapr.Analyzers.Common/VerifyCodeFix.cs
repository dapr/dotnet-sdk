// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Dapr.Analyzers.Common;

internal static class VerifyCodeFix
{
    public static async Task RunTest<T>(
        string code,
        string expectedChangedCode,
        string assemblyLocation,
        IReadOnlyList<MetadataReference> metadataReferences,
        ImmutableArray<DiagnosticAnalyzer> analyzers) where T : CodeFixProvider, new()
    {
        var (diagnostics, document, workspace) =
            await TestUtilities.GetDiagnosticsAdvanced(code, assemblyLocation, metadataReferences, analyzers);

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

        var updatedDocument = workspace.CurrentSolution.GetDocument(document.Id) ??
                              throw new Exception("Updated document is null");
        var newCode = (await updatedDocument.GetTextAsync()).ToString();

        var normalizedExpectedCode = NormalizeWhitespace(expectedChangedCode);
        var normalizedNewCode = NormalizeWhitespace(newCode);

        Assert.Equal(normalizedExpectedCode, normalizedNewCode);
        return;

        // Normalize whitespace
        string NormalizeWhitespace(string input)
        {
            char[] separator = [' ', '\r', '\n'];
            return string.Join(" ", input.Split(separator, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
