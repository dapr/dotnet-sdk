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
/// Detects actors that register reminders but don't implement <c>IVirtualActorRemindable</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingRemindableAnalyzer : DiagnosticAnalyzer
{
    private const string VirtualActorBaseFullName = "Dapr.VirtualActors.Runtime.VirtualActor";
    private const string IRemindableFullName = "Dapr.VirtualActors.IVirtualActorRemindable";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AnalyzerDiagnostics.MissingRemindableInterface);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null || classSymbol.IsAbstract)
            return;

        var virtualActorBase = context.Compilation.GetTypeByMetadataName(VirtualActorBaseFullName);
        if (virtualActorBase is null)
            return;

        // Check if class inherits from VirtualActor
        var inheritsFromVirtualActor = false;
        for (var t = classSymbol.BaseType; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, virtualActorBase))
            {
                inheritsFromVirtualActor = true;
                break;
            }
        }

        if (!inheritsFromVirtualActor)
            return;

        // Check if this actor calls RegisterReminderAsync anywhere
        var body = classDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv =>
            {
                if (inv.Expression is MemberAccessExpressionSyntax memberAccess)
                    return memberAccess.Name.Identifier.Text == "RegisterReminderAsync";
                if (inv.Expression is IdentifierNameSyntax identifier)
                    return identifier.Identifier.Text == "RegisterReminderAsync";
                return false;
            });

        if (!body)
            return;

        // Check if class implements IVirtualActorRemindable
        var iRemindable = context.Compilation.GetTypeByMetadataName(IRemindableFullName);
        if (iRemindable is null)
            return;

        var implementsRemindable = classSymbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, iRemindable));

        if (!implementsRemindable)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDiagnostics.MissingRemindableInterface,
                classDecl.Identifier.GetLocation(),
                classSymbol.Name));
        }
    }
}
