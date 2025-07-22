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

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Actors.Analyzers;

internal class SharedUtilities
{
    internal static InvocationExpressionSyntax? FindInvocation(SyntaxNodeAnalysisContext context, string methodName)
    {
        foreach (var syntaxTree in context.SemanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Name.Identifier.Text == methodName);

            if (invocation != null)
            {
                return invocation;
            }
        }

        return null;
    }
}
