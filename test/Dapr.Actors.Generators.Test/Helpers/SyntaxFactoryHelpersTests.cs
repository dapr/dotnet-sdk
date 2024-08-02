using Dapr.Actors.Generators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Actors.Generators.Test.Helpers
{
    public class SyntaxFactoryHelpersTests
    {
        [Fact]
        public void ThrowArgumentNullExceptionSyntax_GenerateThrowArgumentNullExceptionSyntaxWithGivenArgumentName()
        {
            // Arrange
            var argumentName = "arg0";
            var expectedSource = $@"throw new System.ArgumentNullException(nameof(arg0));";

            // Act
            var generatedSource = SyntaxFactory.ExpressionStatement(SyntaxFactoryHelpers.ThrowArgumentNullExceptionSyntax(argumentName))
                .SyntaxTree
                .GetRoot()
                .NormalizeWhitespace(eol: "\r\n")
                .ToFullString();

            // Assert
            Assert.Equal(expectedSource, generatedSource);
        }

        [Fact]
        public void ThrowIfArgumentNullExceptionSyntax_GivesNullCheckSyntaxWithGivenArgumentName()
        {
            // Arrange
            var argumentName = "arg0";
            var expectedSource = $@"if (arg0 is null)
{{
    throw new System.ArgumentNullException(nameof(arg0));
}}";

            // Act
            var generatedSource = SyntaxFactoryHelpers.ThrowIfArgumentNullSyntax(argumentName)
                .SyntaxTree
                .GetRoot()
                .NormalizeWhitespace(eol: "\r\n")
                .ToFullString();

            // Assert
            Assert.Equal(expectedSource, generatedSource);
        }

        [Fact]
        public void NameOfExpression()
        {
            // Arrange
            var argumentName = "arg0";
            var expectedSource = $@"nameof(arg0)";

            // Act
            var generatedSource = SyntaxFactoryHelpers.NameOfExpression(argumentName)
                .NormalizeWhitespace(eol: "\r\n")
                .ToFullString();

            // Assert
            Assert.Equal(expectedSource, generatedSource);
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
}
