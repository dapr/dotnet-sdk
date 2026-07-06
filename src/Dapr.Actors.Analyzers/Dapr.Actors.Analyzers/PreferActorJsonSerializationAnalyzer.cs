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
/// An analyzer for Dapr Actors that suggests configuring JSON serialization during Actor DI registration for
/// better interoperability with non-.NET actors throughout a Dapr project.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferActorJsonSerializationAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor DiagnosticDescriptorJsonSerialization = new(
        id: "DAPR1403",
        title: new LocalizableResourceString(nameof(Resources.DAPR1403Title), Resources.ResourceManager,
            typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1403MessageFormat), Resources.ResourceManager,
            typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptorJsonSerialization
    );

    /// <summary>
    /// Called once at session start to register actions in the analysis context.
    /// </summary>
    /// <param name="context"></param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSerialization, SyntaxKind.CompilationUnit);
    }

    private static void AnalyzeSerialization(SyntaxNodeAnalysisContext context)
    {
        var addActorsInvocation = SharedUtilities.FindInvocation(context, "AddActors");

        if (addActorsInvocation is null)
        {
            return;
        }

        var optionsLambda = addActorsInvocation.ArgumentList.Arguments
            .Select(arg => arg.Expression)
            .OfType<SimpleLambdaExpressionSyntax>()
            .FirstOrDefault();

        if (optionsLambda == null)
        {
            return;
        }

        var lambdaBody = optionsLambda.Body;
        var assignments = lambdaBody.DescendantNodes().OfType<AssignmentExpressionSyntax>();

        var useJsonSerialization = assignments.Any(assignment =>
            assignment.Left is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == "UseJsonSerialization" &&
            assignment.Right is LiteralExpressionSyntax literal &&
            literal.Token.ValueText == "true");

        if (useJsonSerialization)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(DiagnosticDescriptorJsonSerialization, addActorsInvocation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}
