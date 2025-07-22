using Dapr.Actors.Generators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Actors.Generators.Test.Helpers;

public class SyntaxFactoryHelpersTests
{
    [Fact]
    public void ThrowArgumentNullException_GenerateThrowArgumentNullExceptionSyntaxWithGivenArgumentName()
    {
        // Arrange
        var argumentName = "arg0";
        var expectedSource = $@"throw new System.ArgumentNullException(nameof(arg0));";
        var expectedSourceNormalized = SyntaxFactory.ParseSyntaxTree(expectedSource)
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString();

        // Act
        var generatedSource = SyntaxFactory.ExpressionStatement(SyntaxFactoryHelpers.ThrowArgumentNullException(argumentName))
            .SyntaxTree
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString();

        // Assert
        Assert.Equal(expectedSourceNormalized, generatedSource);
    }

    [Fact]
    public void ThrowIfArgumentNullException_GivesNullCheckSyntaxWithGivenArgumentName()
    {
        // Arrange
        var argumentName = "arg0";
        var expectedSource = $@"if (arg0 is null)
{{
    throw new System.ArgumentNullException(nameof(arg0));
}}";
        var expectedSourceNormalized = SyntaxFactory.ParseSyntaxTree(expectedSource)
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString();

        // Act
        var generatedSource = SyntaxFactoryHelpers.ThrowIfArgumentNull(argumentName)
            .SyntaxTree
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString();

        // Assert
        Assert.Equal(expectedSourceNormalized, generatedSource);
    }

    [Fact]
    public void ActorProxyInvokeMethodAsync_WithoutReturnTypeAndParamters_ReturnNonGenericInvokeMethodAsync()
    {
        // Arrange
        var remoteMethodName = "RemoteMethodToCall";
        var remoteMethodParameters = Array.Empty<IParameterSymbol>();
        var remoteMethodReturnTypes = Array.Empty<ITypeSymbol>();
        var actorProxMemberAccessSyntax = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ThisExpression(),
            SyntaxFactory.IdentifierName("actorProxy")
        );
        var expectedSource = $@"this.actorProxy.InvokeMethodAsync(""RemoteMethodToCall"")";
        var expectedSourceNormalized = SyntaxFactory.ParseSyntaxTree(expectedSource)
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString();

        // Act
        var generatedSource = SyntaxFactoryHelpers.ActorProxyInvokeMethodAsync(
                actorProxMemberAccessSyntax,
                remoteMethodName,
                remoteMethodParameters,
                remoteMethodReturnTypes)
            .SyntaxTree
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString(); ;

        // Assert
        Assert.Equal(expectedSourceNormalized, generatedSource);
    }

    [Fact]
    public void NameOfExpression()
    {
        // Arrange
        var argumentName = "arg0";
        var expectedSource = $@"nameof(arg0)";
        var expectedSourceNormalized = SyntaxFactory.ParseSyntaxTree(expectedSource)
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString();

        // Act
        var generatedSource = SyntaxFactoryHelpers.NameOfExpression(argumentName)
            .SyntaxTree
            .GetRoot()
            .NormalizeWhitespace()
            .ToFullString();

        // Assert
        Assert.Equal(expectedSourceNormalized, generatedSource);
    }

    [Theory]
    [InlineData(Accessibility.Public, new[] { SyntaxKind.PublicKeyword })]
    [InlineData(Accessibility.Internal, new[] { SyntaxKind.InternalKeyword })]
    [InlineData(Accessibility.Private, new[] { SyntaxKind.PrivateKeyword })]
    [InlineData(Accessibility.Protected, new[] { SyntaxKind.ProtectedKeyword })]
    [InlineData(Accessibility.ProtectedAndInternal, new[] { SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword })]
    public void GetSyntaxKinds_GenerateSyntaxForGivenAccessibility(Accessibility accessibility, ICollection<SyntaxKind> expectedSyntaxKinds)
    {
        // Arrange

        // Act
        var generatedSyntaxKinds = SyntaxFactoryHelpers.GetSyntaxKinds(accessibility);

        // Assert
        foreach (var expectedSyntaxKind in expectedSyntaxKinds)
        {
            Assert.Contains(expectedSyntaxKind, generatedSyntaxKinds);
        }

        Assert.Equal(expectedSyntaxKinds.Count, generatedSyntaxKinds.Count);
    }
}