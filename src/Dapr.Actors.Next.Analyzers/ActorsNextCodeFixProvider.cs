// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.Actors.Next.Analyzers;

/// <summary>
/// Provides practical fixes and scaffolds for Dapr Actors Next analyzer diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ActorsNextCodeFixProvider))]
[Shared]
public sealed class ActorsNextCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic ids this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
        "DAPR1410",
        "DAPR1411",
        "DAPR1412",
        "DAPR1413",
        "DAPR1414",
        "DAPR1415",
        "DAPR1416",
        "DAPR1418",
        "DAPR1421",
        "DAPR1423",
        "DAPR1425",
        "DAPR1429");

    /// <summary>
    /// Gets the fix-all provider.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => null!;

    /// <summary>
    /// Registers available fixes for the diagnostic.
    /// </summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case "DAPR1429":
                    await RegisterClosestScheduledCallbackFixAsync(context, diagnostic).ConfigureAwait(false);
                    break;
                case "DAPR1410":
                    RegisterSolutionFix(context, diagnostic, "Promote current state baseline", PromoteBaselineAsync);
                    RegisterDocumentFix(context, diagnostic, "Scaffold actor state upcaster", ScaffoldStateUpcasterAsync);
                    break;
                case "DAPR1411":
                    RegisterDocumentFix(context, diagnostic, "Inline scheduled work", InlineScheduledWorkAsync);
                    break;
                case "DAPR1412":
                    RegisterDocumentFix(context, diagnostic, "Await instead of blocking", AwaitInsteadOfBlockingAsync);
                    break;
                case "DAPR1413":
                    RegisterDocumentFix(context, diagnostic, "Use TimeProvider", UseTimeProviderAsync);
                    break;
                case "DAPR1414":
                    RegisterDocumentFix(context, diagnostic, "Use seeded deterministic source", UseSeededSourceAsync);
                    break;
                case "DAPR1415":
                    RegisterDocumentFix(context, diagnostic, "Scaffold missing upcaster hop", ScaffoldMissingUpcasterAsync);
                    break;
                case "DAPR1416":
                    RegisterDocumentFix(context, diagnostic, "Replace filter logic with actor handoff", ReplaceFilterLogicAsync);
                    break;
                case "DAPR1418":
                    RegisterDocumentFix(context, diagnostic, "Bump actor contract version", BumpContractVersionAsync);
                    RegisterSolutionFix(context, diagnostic, "Promote current wire baseline", PromoteBaselineAsync);
                    break;
                case "DAPR1421":
                    RegisterDocumentFix(context, diagnostic, "Add generated actor client contract", AddGeneratedActorClientContractAsync);
                    break;
                case "DAPR1423":
                    RegisterDocumentFix(context, diagnostic, "Scaffold state family mapping", ScaffoldMissingUpcasterAsync);
                    break;
                case "DAPR1425":
                    RegisterDocumentFix(context, diagnostic, "Scaffold required upcaster", ScaffoldMissingUpcasterAsync);
                    break;
            }
        }
    }

    private static async Task RegisterClosestScheduledCallbackFixAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue("callback.candidates", out var candidatesText) ||
            string.IsNullOrWhiteSpace(candidatesText))
        {
            return;
        }

        var candidates = candidatesText!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (candidates.Length == 0)
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var current = ScheduledCallbackText(node);
        if (current is null)
        {
            return;
        }

        var best = NearestCandidate(current, candidates);
        if (best is null)
        {
            return;
        }

        var title = "Change callback to '" + best + "'";
        context.RegisterCodeFix(
            CodeAction.Create(title, token => ReplaceScheduledCallbackAsync(context.Document, diagnostic, best, token), title),
            diagnostic);
    }

    private static async Task<Document> ReplaceScheduledCallbackAsync(Document document, Diagnostic diagnostic, string replacement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        ExpressionSyntax? newNode = node switch
        {
            LiteralExpressionSyntax =>
                SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(replacement)),
            InvocationExpressionSyntax invocation when IsNameOf(invocation) =>
                invocation.WithArgumentList(SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(replacement))))),
            _ => null,
        };

        return newNode is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(node, newNode.WithTriviaFrom(node)));
    }

    private static string? ScheduledCallbackText(SyntaxNode node) => node switch
    {
        LiteralExpressionSyntax { Token.Value: string value } => value,
        InvocationExpressionSyntax invocation when IsNameOf(invocation) && invocation.ArgumentList.Arguments.Count == 1 =>
            LastIdentifier(invocation.ArgumentList.Arguments[0].Expression),
        _ => null,
    };

    private static string? LastIdentifier(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
        _ => null,
    };

    private static bool IsNameOf(InvocationExpressionSyntax invocation) =>
        invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" };

    private static string? NearestCandidate(string current, IReadOnlyList<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = LevenshteinDistance(current, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        var threshold = Math.Max(3, current.Length / 2);
        return best is not null && bestDistance <= threshold ? best : null;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private static void RegisterDocumentFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        string title,
        Func<Document, Diagnostic, CancellationToken, Task<Document>> fix)
    {
        context.RegisterCodeFix(
            CodeAction.Create(title, token => fix(context.Document, diagnostic, token), title),
            diagnostic);
    }

    private static void RegisterSolutionFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        string title,
        Func<Solution, Diagnostic, CancellationToken, Task<Solution>> fix)
    {
        context.RegisterCodeFix(
            CodeAction.Create(title, token => fix(context.Document.Project.Solution, diagnostic, token), title),
            diagnostic);
    }

    private static async Task<Document> InlineScheduledWorkAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Run" }, ArgumentList.Arguments.Count: > 0 } invocation)
        {
            var expression = UnwrapDelegate(invocation.ArgumentList.Arguments[0].Expression);
            return document.WithSyntaxRoot(root.ReplaceNode(invocation, expression.WithTriviaFrom(invocation)));
        }

        return document;
    }

    private static ExpressionSyntax UnwrapDelegate(ExpressionSyntax expression) =>
        expression switch
        {
            ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax body } => body,
            SimpleLambdaExpressionSyntax { Body: ExpressionSyntax simpleBody } => simpleBody,
            AnonymousMethodExpressionSyntax { Body: BlockSyntax { Statements.Count: 1 } block } when
                block.Statements[0] is ReturnStatementSyntax { Expression: not null } returnStatement => returnStatement
                    .Expression,
            _ => expression
        };

    private static async Task<Document> AwaitInsteadOfBlockingAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        switch (node)
        {
            case MemberAccessExpressionSyntax { Name.Identifier.Text: "Result" } resultAccess:
            {
                var awaitExpression = SyntaxFactory.AwaitExpression(resultAccess.Expression.WithoutTrivia()).WithTriviaFrom(resultAccess);
                return document.WithSyntaxRoot(root.ReplaceNode(resultAccess, awaitExpression));
            }
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Wait" } waitAccess } invocation:
            {
                var awaitExpression = SyntaxFactory.AwaitExpression(waitAccess.Expression.WithoutTrivia()).WithTriviaFrom(invocation);
                return document.WithSyntaxRoot(root.ReplaceNode(invocation, awaitExpression));
            }
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Thread" }, Name.Identifier.Text: "Sleep" } } sleepInvocation:
            {
                var delay = SyntaxFactory.ParseExpression("await Task.Delay(" + sleepInvocation.ArgumentList.Arguments.ToFullString() + ")");
                return document.WithSyntaxRoot(root.ReplaceNode(sleepInvocation, delay.WithTriviaFrom(sleepInvocation)));
            }
            default:
                return document;
        }
    }

    private static async Task<Document> UseTimeProviderAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var replacement = node.ToString() switch
        {
            "DateTime.Now" => "TimeProvider.System.GetLocalNow().DateTime",
            "DateTime.UtcNow" => "TimeProvider.System.GetUtcNow().UtcDateTime",
            "DateTimeOffset.Now" => "TimeProvider.System.GetLocalNow()",
            "DateTimeOffset.UtcNow" => "TimeProvider.System.GetUtcNow()",
            "Stopwatch.StartNew()" => "TimeProvider.System.GetTimestamp()",
            _ => null,
        };

        if (replacement is null)
            return document;

        var expression = SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(node);
        return document.WithSyntaxRoot(root.ReplaceNode(node, expression));
    }

    private static async Task<Document> UseSeededSourceAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var replacement = node.ToString() switch
        {
            "new Random()" => "new Random(0)",
            "Random.Shared" => "new Random(0)",
            "Guid.NewGuid()" => "Guid.Empty",
            _ => null,
        };

        return replacement is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(node,
                SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(node)));
    }

    private static async Task<Document> ScaffoldMissingUpcasterAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var fromType = diagnostic.Properties.TryGetValue("upcaster.from", out var from) ? from : "FromState";
        var toType = diagnostic.Properties.TryGetValue("upcaster.to", out var to) ? to : "ToState";
        var body = "throw new NotImplementedException()";
        if (diagnostic.Properties.TryGetValue("upcaster.copiedMembers", out var copiedMembers) &&
            !string.IsNullOrWhiteSpace(copiedMembers))
        {
            var assignments = copiedMembers!
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static member => "            " + member + " = state." + member + ",");
            body = "ValueTask.FromResult(new " + toType + "\n        {\n" + string.Join("\n", assignments) + "\n        })";
        }

        return await AppendTextAsync(document, "\n" + $@"
public sealed class {SimpleName(fromType)}To{SimpleName(toType)}Upcaster : Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<{fromType}, {toType}>
{{
    public ValueTask<{toType}> UpcastAsync({fromType} state, CancellationToken cancellationToken = default) =>
        {body};
}}
").ConfigureAwait(false);
    }

    private static async Task<Document> ScaffoldStateUpcasterAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var stateName = diagnostic.Properties.TryGetValue("baseline.name", out var name) ? name : "ActorState";
        return await AppendTextAsync(document, "\n" + $@"
public sealed class {SimpleName(stateName)}Upcaster : Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<{stateName}, {stateName}>
{{
    public ValueTask<{stateName}> UpcastAsync({stateName} state, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}}
").ConfigureAwait(false);
    }

    private static async Task<Document> ReplaceFilterLogicAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var statement = node.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
        if (statement is null)
        {
            return document;
        }

        var newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> BumpContractVersionAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var actorAttribute = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .FirstOrDefault(static a => a.Name.ToString().EndsWith("DaprActor", StringComparison.Ordinal) ||
                                        a.Name.ToString().EndsWith("DaprActorAttribute", StringComparison.Ordinal));
        if (actorAttribute is null)
        {
            return document;
        }

        var argumentList = actorAttribute.ArgumentList ?? SyntaxFactory.AttributeArgumentList();
        var existingVersion = argumentList.Arguments.FirstOrDefault(static a => a.NameEquals?.Name.Identifier.Text == "ContractVersion");
        AttributeSyntax newAttribute;
        if (existingVersion is not null)
        {
            var current = existingVersion.Expression is LiteralExpressionSyntax literal && literal.Token.Value is int value ? value : 1;
            var newArgument = existingVersion.WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(current + 1)));
            newAttribute = actorAttribute.WithArgumentList(argumentList.WithArguments(argumentList.Arguments.Replace(existingVersion, newArgument)));
        }
        else
        {
            var newArgument = SyntaxFactory.AttributeArgument(
                SyntaxFactory.NameEquals("ContractVersion"),
                nameColon: null,
                expression: SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(2)));
            var newArguments = argumentList.Arguments.Add(newArgument);
            newAttribute = actorAttribute.WithArgumentList(argumentList.WithArguments(newArguments));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(actorAttribute, newAttribute));
    }

    private static async Task<Document> AddGeneratedActorClientContractAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var classDeclaration = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration is null ||
            semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not { } actorType)
        {
            return document;
        }

        var actorInterface = actorType.AllInterfaces
            .Where(static candidate => candidate.ToDisplayString() != "Dapr.Actors.Next.Abstractions.IActor" &&
                                       candidate.Implements("Dapr.Actors.Next.Abstractions.IActor"))
            .OrderBy(static candidate => candidate.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
            .FirstOrDefault();
        if (actorInterface is not null)
        {
            return DecorateActorInterface(document, root, semanticModel, actorInterface, cancellationToken);
        }

        var interfaceName = SuggestedActorInterfaceName(actorType.Name);
        var updatedClass = AddBaseType(classDeclaration, interfaceName);
        var annotation = new SyntaxAnnotation();
        updatedClass = updatedClass.WithAdditionalAnnotations(annotation);

        var replacedRoot = root.ReplaceNode(classDeclaration, updatedClass);
        var insertedAfter = replacedRoot.GetAnnotatedNodes(annotation).OfType<ClassDeclarationSyntax>().SingleOrDefault();
        if (insertedAfter is null)
        {
            return WithRootPreservingNewLines(document, root, replacedRoot);
        }

        var interfaceDeclaration = SyntaxFactory.ParseMemberDeclaration($@"
[{GenerateActorClientAttributeName(root)}]
public interface {interfaceName} : {ActorInterfaceName(root)}
{{
}}
")!;

        return WithRootPreservingNewLines(document, root, replacedRoot.InsertNodesAfter(insertedAfter, [interfaceDeclaration]));
    }

    private static Document DecorateActorInterface(
        Document document,
        SyntaxNode root,
        SemanticModel semanticModel,
        INamedTypeSymbol actorInterface,
        CancellationToken cancellationToken)
    {
        foreach (var interfaceDeclaration in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(interfaceDeclaration, cancellationToken) is not { } declared ||
                !SymbolEqualityComparer.Default.Equals(declared.OriginalDefinition, actorInterface.OriginalDefinition))
            {
                continue;
            }

            var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName(GenerateActorClientAttributeName(root)))))
                .WithLeadingTrivia(interfaceDeclaration.GetLeadingTrivia())
                .WithTrailingTrivia(NewLineTrivia(root));
            var newInterface = interfaceDeclaration
                .WithLeadingTrivia()
                .AddAttributeLists(attributeList);
            return WithRootPreservingNewLines(document, root, root.ReplaceNode(interfaceDeclaration, newInterface));
        }

        return document;
    }

    private static Document WithRootPreservingNewLines(Document document, SyntaxNode oldRoot, SyntaxNode newRoot)
    {
        var newText = newRoot.ToFullString();
        var withoutCrLf = newText.Replace("\r\n", string.Empty);
        return newText.Contains("\r\n", StringComparison.Ordinal) &&
            withoutCrLf.Contains('\n')
            ? document.WithText(SourceText.From(newText.Replace("\r\n", "\n")))
            : document.WithSyntaxRoot(newRoot);
    }

    private static ClassDeclarationSyntax AddBaseType(ClassDeclarationSyntax classDeclaration, string interfaceName)
    {
        var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(interfaceName));
        return classDeclaration.BaseList is null
            ? classDeclaration.WithBaseList(SyntaxFactory.BaseList(
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType)))
            : classDeclaration.WithBaseList(classDeclaration.BaseList.WithTypes(AppendBaseType(classDeclaration.BaseList.Types, baseType)));
    }

    private static SeparatedSyntaxList<BaseTypeSyntax> AppendBaseType(SeparatedSyntaxList<BaseTypeSyntax> types, BaseTypeSyntax baseType)
    {
        var nodesAndTokens = new List<SyntaxNodeOrToken>();
        nodesAndTokens.AddRange(types.GetWithSeparators());
        nodesAndTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space));
        nodesAndTokens.Add(baseType);
        return SyntaxFactory.SeparatedList<BaseTypeSyntax>(nodesAndTokens);
    }

    private static string GenerateActorClientAttributeName(SyntaxNode root) =>
        HasUsing(root, "Dapr.Actors.Next.Abstractions.Attributes")
            ? "GenerateActorClient"
            : "Dapr.Actors.Next.Abstractions.Attributes.GenerateActorClient";

    private static string ActorInterfaceName(SyntaxNode root) =>
        HasUsing(root, "Dapr.Actors.Next.Abstractions")
            ? "IActor"
            : "Dapr.Actors.Next.Abstractions.IActor";

    private static bool HasUsing(SyntaxNode root, string namespaceName) =>
        root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(usingDirective =>
            StringComparer.Ordinal.Equals(usingDirective.Name?.ToString(), namespaceName));

    private static SyntaxTrivia NewLineTrivia(SyntaxNode root)
    {
        var text = root.SyntaxTree.GetText().ToString();
        return SyntaxFactory.EndOfLine(text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
    }

    private static string SuggestedActorInterfaceName(string actorTypeName)
    {
        var contractName = actorTypeName.EndsWith("Actor", StringComparison.Ordinal) && actorTypeName.Length > "Actor".Length
            ? actorTypeName.Substring(0, actorTypeName.Length - "Actor".Length) + "Actor"
            : actorTypeName;
        return contractName.StartsWith("I", StringComparison.Ordinal) ? contractName : "I" + contractName;
    }

    private static async Task<Solution> PromoteBaselineAsync(Solution solution, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        if (!diagnostic.Properties.TryGetValue("baseline.current", out var currentLine) || string.IsNullOrWhiteSpace(currentLine) ||
            !diagnostic.Properties.TryGetValue("baseline.kind", out var kind) ||
            !diagnostic.Properties.TryGetValue("baseline.name", out var name))
        {
            return solution;
        }

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.AdditionalDocuments)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(document.FilePath), ActorBaseline.ShippedFileName))
                {
                    continue;
                }

                var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var lines = text.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
                var prefix = kind + "|" + name + "|";
                var replaced = false;
                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(prefix, StringComparison.Ordinal))
                    {
                        lines[i] = currentLine;
                        replaced = true;
                    }
                }

                if (!replaced)
                {
                    lines.Add(currentLine);
                }

                return solution.WithAdditionalDocumentText(document.Id, SourceText.From(string.Join("\n", lines)));
            }
        }

        return solution;
    }

    private static async Task<Document> AppendTextAsync(Document document, string text)
    {
        var sourceText = await document.GetTextAsync().ConfigureAwait(false);
        var source = sourceText.ToString();
        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var newText = source + text.Replace("\n", newline);
        return document.WithText(SourceText.From(newText));
    }

    private static string SimpleName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "ActorState";
        }

        var index = fullName!.LastIndexOf('.');
        return index < 0 ? fullName : fullName.Substring(index + 1);
    }
}
