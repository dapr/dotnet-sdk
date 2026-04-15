// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.VirtualActors.Analyzers;

/// <summary>
/// Analyzes actor interface methods to ensure they return <c>Task</c> or <c>Task{T}</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActorMethodReturnTypeAnalyzer : DiagnosticAnalyzer
{
    private const string IVirtualActorFullName = "Dapr.VirtualActors.IVirtualActor";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AnalyzerDiagnostics.ActorMethodMustReturnTask);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInterface, SyntaxKind.InterfaceDeclaration);
    }

    private static void AnalyzeInterface(SyntaxNodeAnalysisContext context)
    {
        var interfaceDecl = (InterfaceDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(interfaceDecl);
        if (symbol is null)
            return;

        // Check if this interface derives from IVirtualActor
        var iVirtualActor = context.Compilation.GetTypeByMetadataName(IVirtualActorFullName);
        if (iVirtualActor is null)
            return;

        var implementsIVirtualActor = symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iVirtualActor)) ||
            SymbolEqualityComparer.Default.Equals(symbol, iVirtualActor);

        if (!implementsIVirtualActor)
            return;

        // Don't analyze IVirtualActor itself
        if (SymbolEqualityComparer.Default.Equals(symbol, iVirtualActor))
            return;

        var taskSymbol = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var taskOfTSymbol = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

        foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind != MethodKind.Ordinary)
                continue;

            var returnType = member.ReturnType;

            var isTask = SymbolEqualityComparer.Default.Equals(returnType, taskSymbol);
            var isTaskOfT = returnType.OriginalDefinition is INamedTypeSymbol namedReturn &&
                            SymbolEqualityComparer.Default.Equals(namedReturn, taskOfTSymbol);

            if (!isTask && !isTaskOfT)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AnalyzerDiagnostics.ActorMethodMustReturnTask,
                    member.Locations.FirstOrDefault() ?? Location.None,
                    member.Name));
            }
        }
    }
}
