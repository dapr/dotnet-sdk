using Microsoft.CodeAnalysis;
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
        public static ThrowExpressionSyntax ThrowArgumentNullExceptionSyntax(string argumentName)
        {
            return SyntaxFactory.ThrowExpression(
                SyntaxFactory.Token(SyntaxKind.ThrowKeyword),
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.Token(SyntaxKind.NewKeyword),
                    SyntaxFactory.ParseTypeName("System.ArgumentNullException"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.IdentifierName("nameof"),
                                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                                {
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(argumentName))
                                }))
                            )
                        )
                    })),
                    default
                )
            );
        }

        /// <summary>
        /// Generates a syntax for null check for the given argument name.
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        public static IfStatementSyntax ThrowIfArgumentNullSyntax(string argumentName)
        {
            return SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(
                    SyntaxKind.IsExpression,
                    SyntaxFactory.IdentifierName(argumentName),
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                ),
                SyntaxFactory.Block(SyntaxFactory.List(new StatementSyntax[]
                {
                    SyntaxFactory.ExpressionStatement(ThrowArgumentNullExceptionSyntax(argumentName))
                }))
            );
        }

        /// <summary>
        /// Returns the syntax kinds for the specified accessibility.
        /// </summary>
        /// <param name="accessibility"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static ICollection<SyntaxKind> GetSyntaxKinds(Accessibility accessibility)
        {
            var syntaxKinds = new List<SyntaxKind>();

            switch (accessibility)
            {
                case Accessibility.Public:
                    syntaxKinds.Add(SyntaxKind.PublicKeyword);
                    break;
                case Accessibility.Internal:
                    syntaxKinds.Add(SyntaxKind.InternalKeyword);
                    break;
                case Accessibility.Private:
                    syntaxKinds.Add(SyntaxKind.PrivateKeyword);
                    break;
                case Accessibility.Protected:
                    syntaxKinds.Add(SyntaxKind.ProtectedKeyword);
                    break;
                case Accessibility.ProtectedAndInternal:
                    syntaxKinds.Add(SyntaxKind.ProtectedKeyword);
                    syntaxKinds.Add(SyntaxKind.InternalKeyword);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected accessibility");
            }

            return syntaxKinds;
        }
    }
}
