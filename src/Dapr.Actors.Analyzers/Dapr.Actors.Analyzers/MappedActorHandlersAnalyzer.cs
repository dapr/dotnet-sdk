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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// An analyzer for Dapr actors that validates that the handler is set up during initial setup to map
/// the actor endpoints.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MappedActorHandlersAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor DiagnosticDescriptorMapActorsHandlers = new(
        id: "DAPR1404",
        title: new LocalizableResourceString(nameof(Resources.DAPR1404Title), Resources.ResourceManager,
            typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1404MessageFormat), Resources.ResourceManager,
            typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    /// <summary>
    /// Returns a set of descriptors for the diagnostics that this analyzer is capable of producing.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptorMapActorsHandlers);

    /// <summary>
    /// Called once at session start to register actions in the analysis context.
    /// </summary>
    /// <param name="context"></param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMapActorsHandlers, SyntaxKind.CompilationUnit);
    }
    
    private static void AnalyzeMapActorsHandlers(SyntaxNodeAnalysisContext context)
    {
        var addActorsInvocation = SharedUtilities.FindInvocation(context, "AddActors");

        if (addActorsInvocation == null)
        {
            return;
        }

        var invokedByWebApplication = false;
        var mapActorsHandlersInvocation = SharedUtilities.FindInvocation(context, "MapActorsHandlers");

        if (mapActorsHandlersInvocation?.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var symbolInfo = ModelExtensions.GetSymbolInfo(context.SemanticModel, memberAccess.Expression);
            if (symbolInfo.Symbol is ILocalSymbol localSymbol)
            {
                var type = localSymbol.Type;
                if (type.ToDisplayString() == "Microsoft.AspNetCore.Builder.WebApplication")
                {
                    invokedByWebApplication = true;
                }
            }
        }

        if (mapActorsHandlersInvocation != null && invokedByWebApplication)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapActorsHandlers, addActorsInvocation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}
