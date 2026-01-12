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
/// An analyzer for Dapr actors that validates that each discovered Actor implementation is properly registered with
/// dependency injection during startup.
/// </summary>
[DiagnosticAnalyzer((LanguageNames.CSharp))]
public sealed class ActorRegistrationAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor DiagnosticDescriptorActorRegistration = new(
        id: "DAPR1402",
        title: new LocalizableResourceString(nameof(Resources.DAPR1402Title), Resources.ResourceManager,
            typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1402MessageFormat), Resources.ResourceManager,
            typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    /// <summary>
    /// Returns a set of descriptors for the diagnostics that this analyzer is capable of producing.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptorActorRegistration);
    
    /// <summary>
    /// Called once at session start to register actions in the analysis context.
    /// </summary>
    /// <param name="context"></param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeActorRegistration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeActorRegistration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (classDeclaration.BaseList == null)
        {
            return;
        }

        var baseTypeSyntax = classDeclaration.BaseList.Types[0].Type;

        if (context.SemanticModel.GetSymbolInfo(baseTypeSyntax).Symbol is not INamedTypeSymbol baseTypeSymbol)
        {
            return;
        }

        var actorTypeName = classDeclaration.Identifier.Text;
        var isRegistered = CheckIfActorIsRegistered(actorTypeName, context.SemanticModel);
        if (isRegistered)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(DiagnosticDescriptorActorRegistration, classDeclaration.Identifier.GetLocation(), actorTypeName);
        context.ReportDiagnostic(diagnostic);
    }
    
    private static bool CheckIfActorIsRegistered(string actorTypeName, SemanticModel semanticModel)
    {
        var methodInvocations = new List<InvocationExpressionSyntax>();
        foreach (var syntaxTree in semanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            methodInvocations.AddRange(root.DescendantNodes().OfType<InvocationExpressionSyntax>());
        }

        foreach (var invocation in methodInvocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName != "RegisterActor")
            {
                continue;
            }

            if (memberAccess.Name is not GenericNameSyntax typeArgumentList ||
                typeArgumentList.TypeArgumentList.Arguments.Count <= 0)
            {
                continue;
            }

            switch (typeArgumentList.TypeArgumentList.Arguments[0])
            {
                case IdentifierNameSyntax typeArgument when typeArgument.Identifier.Text == actorTypeName:
                case QualifiedNameSyntax qualifiedName when qualifiedName.Right.Identifier.Text == actorTypeName:
                    return true;
            }
        }

        return false;
    }
}
