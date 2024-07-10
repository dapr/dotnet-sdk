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
        /// Generates a syntax for ArgumentNullException syntax for the given parameter name.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        public static ThrowExpressionSyntax ArgumentNullExceptionSyntax(string parameterName)
        {
            return SyntaxFactory.ThrowExpression(
                SyntaxFactory.Token(SyntaxKind.ThrowKeyword),
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.ParseTypeName("System.ArgumentNullException"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameterName))
                    }))
                )
            );
        }
    }
}
