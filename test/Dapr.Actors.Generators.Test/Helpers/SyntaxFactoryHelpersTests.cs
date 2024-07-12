﻿using Dapr.Actors.Generators.Helpers;
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
            var expectedSource = $@"throw new System.ArgumentNullException(""arg0"");";

            // Act
            var generatedSource = SyntaxFactory.ExpressionStatement(SyntaxFactoryHelpers.ThrowArgumentNullExceptionSyntax(argumentName))
                .SyntaxTree
                .GetRoot()
                .NormalizeWhitespace()
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
    throw new System.ArgumentNullException(""arg0"");
}}";

            // Act
            var generatedSource = SyntaxFactoryHelpers.ThrowIfArgumentNullSyntax(argumentName)
                .SyntaxTree
                .GetRoot()
                .NormalizeWhitespace()
                .ToFullString();

            // Assert
            Assert.Equal(expectedSource, generatedSource);
        }
    }
}
