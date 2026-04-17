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
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Dapr.VirtualActors.Analyzers;

/// <summary>
/// Provides a code fix for DAPRVACT005 by adding <c>IVirtualActorRemindable</c> to the
/// class declaration and generating the required <c>ReceiveReminderAsync</c> stub.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingRemindableCodeFixProvider))]
[Shared]
public sealed class MissingRemindableCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AnalyzerDiagnostics.MissingRemindableInterface.Id);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var classDecl = root.FindToken(diagnosticSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDecl is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Implement IVirtualActorRemindable",
                createChangedDocument: ct => AddRemindableInterfaceAsync(context.Document, classDecl, ct),
                equivalenceKey: nameof(MissingRemindableCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddRemindableInterfaceAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        // Add IVirtualActorRemindable to the base list
        var newBaseType = SimpleBaseType(
            ParseTypeName("Dapr.VirtualActors.IVirtualActorRemindable"));

        var updatedBases = classDecl.BaseList is not null
            ? classDecl.BaseList.AddTypes(newBaseType)
            : BaseList(SeparatedList<BaseTypeSyntax>(new[] { newBaseType }));

        // Generate the ReceiveReminderAsync stub
        var methodStub = MethodDeclaration(
                ParseTypeName("System.Threading.Tasks.Task"),
                Identifier("ReceiveReminderAsync"))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(Identifier("reminderName")).WithType(ParseTypeName("string")),
                Parameter(Identifier("state")).WithType(ParseTypeName("byte[]?")),
                Parameter(Identifier("dueTime")).WithType(ParseTypeName("System.TimeSpan")),
                Parameter(Identifier("period")).WithType(ParseTypeName("System.TimeSpan")),
                Parameter(Identifier("cancellationToken"))
                    .WithType(ParseTypeName("System.Threading.CancellationToken"))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.DefaultLiteralExpression))))
            .WithBody(Block(
                ReturnStatement(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Task"),
                        IdentifierName("CompletedTask")))))
            .WithLeadingTrivia(TriviaList(
                Whitespace("    "),
                Comment("/// <inheritdoc />"),
                CarriageReturnLineFeed,
                Whitespace("    ")));

        var updatedClass = classDecl
            .WithBaseList(updatedBases)
            .AddMembers(methodStub);

        var newRoot = root.ReplaceNode(classDecl, updatedClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
