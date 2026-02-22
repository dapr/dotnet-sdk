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
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// Provides code fixes for Actor serialization diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ActorSerializationCodeFixProvider)), Shared]
public sealed class ActorSerializationCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            ActorSerializationAnalyzer.ActorInterfaceMissingIActor.Id,
            ActorSerializationAnalyzer.EnumMissingEnumMemberAttribute.Id,
            ActorSerializationAnalyzer.WeaklyTypedActorJsonPropertyRecommendation.Id,
            ActorSerializationAnalyzer.ComplexTypeInActorNeedsAttributes.Id,
            ActorSerializationAnalyzer.RecordTypeNeedsDataContractAttributes.Id
        );

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

            switch (diagnostic.Id)
            {
                case "DAPR1405":
                    RegisterAddIActorInterfaceFix(context, root, node, diagnostic);
                    break;

                case "DAPR1406":
                    RegisterAddEnumMemberFix(context, root, node, diagnostic);
                    break;

                case "DAPR1407":
                    RegisterAddJsonPropertyNameFix(context, root, node, diagnostic);
                    break;

                case "DAPR1408":
                case "DAPR1409":
                case "DAPR1410":
                    RegisterAddDataContractFix(context, root, node, diagnostic);
                    break;

                case "DAPR1412":
                    RegisterAddRecordDataContractFix(context, root, node, diagnostic);
                    break;
            }
        }
    }

    private static void RegisterAddIActorInterfaceFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
    {
        if (node is not InterfaceDeclarationSyntax interfaceDeclaration)
        {
            return;
        }

        var action = CodeAction.Create(
            title: "Add IActor inheritance",
            createChangedDocument: c => AddIActorInheritance(context.Document, root, interfaceDeclaration, c),
            equivalenceKey: "AddIActor");

        context.RegisterCodeFix(action, diagnostic);
    }

    private static void RegisterAddEnumMemberFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
    {
        if (node is not EnumMemberDeclarationSyntax enumMemberDeclaration)
        {
            return;
        }

        var action = CodeAction.Create(
            title: "Add [EnumMember] attribute",
            createChangedDocument: c => AddEnumMemberAttribute(context.Document, root, enumMemberDeclaration, c),
            equivalenceKey: "AddEnumMember");

        context.RegisterCodeFix(action, diagnostic);
    }

    private static void RegisterAddJsonPropertyNameFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
    {
        if (node is not PropertyDeclarationSyntax propertyDeclaration)
        {
            return;
        }

        var action = CodeAction.Create(
            title: "Add [JsonPropertyName] attribute",
            createChangedDocument: c => AddJsonPropertyNameAttribute(context.Document, root, propertyDeclaration, c),
            equivalenceKey: "AddJsonPropertyName");

        context.RegisterCodeFix(action, diagnostic);
    }

    private static void RegisterAddDataContractFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
    {
        if (node is ClassDeclarationSyntax classDeclaration)
        {
            var action = CodeAction.Create(
                title: "Add [DataContract] attribute",
                createChangedDocument: c => AddDataContractAttribute(context.Document, root, classDeclaration, c),
                equivalenceKey: "AddDataContract");

            context.RegisterCodeFix(action, diagnostic);
        }
        else if (node is StructDeclarationSyntax structDeclaration)
        {
            var action = CodeAction.Create(
                title: "Add [DataContract] attribute",
                createChangedDocument: c => AddDataContractAttributeToStruct(context.Document, root, structDeclaration, c),
                equivalenceKey: "AddDataContractStruct");

            context.RegisterCodeFix(action, diagnostic);
        }
    }

    private static void RegisterAddRecordDataContractFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
    {
        if (node is RecordDeclarationSyntax recordDeclaration)
        {
            var action = CodeAction.Create(
                title: "Add [DataContract] and [DataMember] attributes",
                createChangedDocument: c => AddDataContractToRecord(context.Document, root, recordDeclaration, c),
                equivalenceKey: "AddDataContractRecord");

            context.RegisterCodeFix(action, diagnostic);
        }
        else if (node is ParameterSyntax parameter)
        {
            var parentRecord = parameter.Ancestors().OfType<RecordDeclarationSyntax>().FirstOrDefault();
            if (parentRecord != null)
            {
                var action = CodeAction.Create(
                    title: "Add [DataMember] attribute to parameter",
                    createChangedDocument: c => AddDataMemberToRecordParameter(context.Document, root, parameter, c),
                    equivalenceKey: "AddDataMemberParameter");

                context.RegisterCodeFix(action, diagnostic);
            }
        }
    }

    private static Task<Document> AddIActorInheritance(Document document, SyntaxNode root, InterfaceDeclarationSyntax interfaceDeclaration, CancellationToken cancellationToken)
    {
        var iactorType = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName("IActor"));

        BaseListSyntax baseList;
        if (interfaceDeclaration.BaseList == null)
        {
            baseList = SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(iactorType));
        }
        else
        {
            baseList = interfaceDeclaration.BaseList.AddTypes(iactorType);
        }

        var newInterfaceDeclaration = interfaceDeclaration.WithBaseList(baseList);
        var newRoot = root.ReplaceNode(interfaceDeclaration, newInterfaceDeclaration);

        newRoot = AddUsingIfMissing(newRoot, "Dapr.Actors");

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> AddEnumMemberAttribute(Document document, SyntaxNode root, EnumMemberDeclarationSyntax enumMemberDeclaration, CancellationToken cancellationToken)
    {
        var enumMemberAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("EnumMember"));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(enumMemberAttribute));

        var newEnumMemberDeclaration = enumMemberDeclaration.AddAttributeLists(attributeList);
        var newRoot = root.ReplaceNode(enumMemberDeclaration, newEnumMemberDeclaration);

        newRoot = AddUsingIfMissing(newRoot, "System.Runtime.Serialization");

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> AddJsonPropertyNameAttribute(Document document, SyntaxNode root, PropertyDeclarationSyntax propertyDeclaration, CancellationToken cancellationToken)
    {
        var propertyName = propertyDeclaration.Identifier.ValueText;
        if (string.IsNullOrEmpty(propertyName))
        {
            return Task.FromResult(document);
        }

        var camelCaseName = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);

        var jsonPropertyNameAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("JsonPropertyName"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(camelCaseName))))));

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(jsonPropertyNameAttribute));

        var newPropertyDeclaration = propertyDeclaration.AddAttributeLists(attributeList);
        var newRoot = root.ReplaceNode(propertyDeclaration, newPropertyDeclaration);

        newRoot = AddUsingIfMissing(newRoot, "System.Text.Json.Serialization");

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> AddDataContractAttribute(Document document, SyntaxNode root, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
    {
        var dataContractAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("DataContract"));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(dataContractAttribute));

        var newClassDeclaration = classDeclaration.AddAttributeLists(attributeList);
        var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

        newRoot = AddUsingIfMissing(newRoot, "System.Runtime.Serialization");

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> AddDataContractAttributeToStruct(Document document, SyntaxNode root, StructDeclarationSyntax structDeclaration, CancellationToken cancellationToken)
    {
        var dataContractAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("DataContract"));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(dataContractAttribute));

        var newStructDeclaration = structDeclaration.AddAttributeLists(attributeList);
        var newRoot = root.ReplaceNode(structDeclaration, newStructDeclaration);

        newRoot = AddUsingIfMissing(newRoot, "System.Runtime.Serialization");

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> AddDataContractToRecord(Document document, SyntaxNode root, RecordDeclarationSyntax recordDeclaration, CancellationToken cancellationToken)
    {
        var newRoot = root;

        var dataContractAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("DataContract"));
        var dataContractAttributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(dataContractAttribute));

        var newRecordDeclaration = recordDeclaration.AddAttributeLists(dataContractAttributeList);

        if (recordDeclaration.ParameterList != null)
        {
            var newParameters = new List<ParameterSyntax>();

            foreach (var parameter in recordDeclaration.ParameterList.Parameters)
            {
                if (!parameter.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString().Contains("DataMember"))))
                {
                    var dataMemberAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("DataMember"));
                    var dataMemberAttributeList = SyntaxFactory.AttributeList(
                        SyntaxFactory.SingletonSeparatedList(dataMemberAttribute))
                        .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.PropertyKeyword)));

                    newParameters.Add(parameter.AddAttributeLists(dataMemberAttributeList));
                }
                else
                {
                    newParameters.Add(parameter);
                }
            }

            var newParameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newParameters));
            newRecordDeclaration = newRecordDeclaration.WithParameterList(newParameterList);
        }

        newRoot = newRoot.ReplaceNode(recordDeclaration, newRecordDeclaration);

        newRoot = AddUsingIfMissing(newRoot, "System.Runtime.Serialization");

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> AddDataMemberToRecordParameter(Document document, SyntaxNode root, ParameterSyntax parameter, CancellationToken cancellationToken)
    {
        var dataMemberAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("DataMember"));
        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(dataMemberAttribute))
            .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.PropertyKeyword)));

        var newParameter = parameter.AddAttributeLists(attributeList);
        var newRoot = root.ReplaceNode(parameter, newParameter);

        newRoot = AddUsingIfMissing(newRoot, "System.Runtime.Serialization");

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode AddUsingIfMissing(SyntaxNode root, string namespaceName)
    {
        if (root is CompilationUnitSyntax compilationUnit && !HasUsingDirective(compilationUnit, namespaceName))
        {
            var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName));
            return compilationUnit.AddUsings(usingDirective);
        }

        return root;
    }

    private static bool HasUsingDirective(CompilationUnitSyntax compilationUnit, string namespaceName) =>
        compilationUnit.Usings.Any(u => u.Name?.ToString() == namespaceName);
}
