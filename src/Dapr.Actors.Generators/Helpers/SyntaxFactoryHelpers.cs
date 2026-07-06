using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Actors.Generators.Helpers;

/// <summary>
/// Syntax factory helpers for generating syntax.
/// </summary>
internal static partial class SyntaxFactoryHelpers
{
    /// <summary>
    /// Generates a syntax for an <see cref="ArgumentNullException"/> based on the given argument name.
    /// </summary>
    /// <param name="argumentName">Name of the argument that generated the exception.</param>
    /// <returns>Returns <see cref="ThrowExpressionSyntax"/> used to throw an <see cref="ArgumentNullException"/>.</returns>
    public static ThrowExpressionSyntax ThrowArgumentNullException(string argumentName)
    {
        return SyntaxFactory.ThrowExpression(
            SyntaxFactory.Token(SyntaxKind.ThrowKeyword),
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.Token(SyntaxKind.NewKeyword),
                SyntaxFactory.ParseTypeName("System.ArgumentNullException"),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(NameOfExpression(argumentName))
                })),
                default
            )
        );
    }

    /// <summary>
    /// Generates a syntax for null check for the given argument name.
    /// </summary>
    /// <param name="argumentName">Name of the argument whose null check is to be generated.</param>
    /// <returns>Returns <see cref="IfStatementSyntax"/> representing an argument null check.</returns>
    public static IfStatementSyntax ThrowIfArgumentNull(string argumentName)
    {
        return SyntaxFactory.IfStatement(
            SyntaxFactory.BinaryExpression(
                SyntaxKind.IsExpression,
                SyntaxFactory.IdentifierName(argumentName),
                SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
            ),
            SyntaxFactory.Block(SyntaxFactory.List(new StatementSyntax[]
            {
                SyntaxFactory.ExpressionStatement(ThrowArgumentNullException(argumentName))
            }))
        );
    }

    /// <summary>
    /// Generates a syntax for nameof expression for the given argument name.
    /// </summary>
    /// <param name="argumentName">Name of the argument from which the syntax is to be generated.</param>
    /// <returns>Return a <see cref="ExpressionSyntax"/> representing a NameOf expression.</returns>
    public static ExpressionSyntax NameOfExpression(string argumentName)
    {
        var nameofIdentifier = SyntaxFactory.Identifier(
            SyntaxFactory.TriviaList(),
            SyntaxKind.NameOfKeyword,
            "nameof",
            "nameof",
            SyntaxFactory.TriviaList());

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(nameofIdentifier),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName(argumentName))
            }))
        );
    }

    /// <summary>
    /// Generates the invocation syntax to call a remote method with the actor proxy.
    /// </summary>
    /// <param name="actorProxyMemberSyntax">Member syntax to access actorProxy member.</param>
    /// <param name="remoteMethodName">Name of remote method to invoke.</param>
    /// <param name="remoteMethodParameters">Remote method parameters.</param>
    /// <param name="remoteMethodReturnTypes">Return types of remote method invocation.</param>
    /// <returns>Returns the <see cref="InvocationExpressionSyntax"/> representing a call to the actor proxy.</returns>
    public static InvocationExpressionSyntax ActorProxyInvokeMethodAsync(
        MemberAccessExpressionSyntax actorProxyMemberSyntax,
        string remoteMethodName,
        IEnumerable<IParameterSymbol> remoteMethodParameters,
        IEnumerable<ITypeSymbol> remoteMethodReturnTypes)
    {
        // Define the type arguments to pass to the actor proxy method invocation.
        var proxyInvocationTypeArguments = new List<TypeSyntax>()
            .Concat(remoteMethodParameters
                .Where(p => p.Type is not { Name: "CancellationToken" })
                .Select(p => SyntaxFactory.ParseTypeName(p.Type.ToString())))
            .Concat(remoteMethodReturnTypes
                .Select(a => SyntaxFactory.ParseTypeName(a.OriginalDefinition.ToString())));

        // Define the arguments to pass to the actor proxy method invocation.
        var proxyInvocationArguments = new List<ArgumentSyntax>()
            // Name of remote method to invoke.
            .Append(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(remoteMethodName))))
            // Actor method arguments, including the CancellationToken if it exists.
            .Concat(remoteMethodParameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Name))));

        // If the invocation has return types or input parameters, we need to use the generic version of the method.
        SimpleNameSyntax invokeAsyncSyntax = proxyInvocationTypeArguments.Any()
            ? SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("InvokeMethodAsync"),
                SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(proxyInvocationTypeArguments)))
            : SyntaxFactory.IdentifierName("InvokeMethodAsync");

        // Generate the invocation syntax.
        var generatedInvocationSyntax = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    actorProxyMemberSyntax,
                    invokeAsyncSyntax
                ))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(proxyInvocationArguments)));

        return generatedInvocationSyntax;
    }

    /// <summary>
    /// Returns the <see cref="SyntaxKind"/> for the specified accessibility.
    /// </summary>
    /// <param name="accessibility">Accessibility to convert into a <see cref="SyntaxKind"/>.</param>
    /// <returns>Returns the collection of <see cref="SyntaxKind"/> representing the given accessibility.</returns>
    /// <exception cref="InvalidOperationException">Throws when un unexpected accessibility is passed.</exception>
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