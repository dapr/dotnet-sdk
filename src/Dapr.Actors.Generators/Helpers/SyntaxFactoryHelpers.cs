using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Actors.Generators.Helpers
{
    /// <summary>
    /// Syntax factory helpers for generating exception syntax.
    /// </summary>
    public static partial class SyntaxFactoryHelpers
    {
        /// <summary>
        /// Generates a syntax for <see cref="ArgumentNullException"></see> syntax for the given argument name.
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        public static ThrowExpressionSyntax ArgumentNullExceptionSyntax(string argumentName)
        {
            return SyntaxFactory.ThrowExpression(
                SyntaxFactory.Token(SyntaxKind.ThrowKeyword),
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.Token(SyntaxKind.NewKeyword),
                    SyntaxFactory.ParseTypeName("System.ArgumentNullException"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(argumentName)))
                    })),
                    default
                )
            );
        }
    }
}
