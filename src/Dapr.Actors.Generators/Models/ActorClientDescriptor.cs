using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators.Models;

/// <summary>
/// Describes an actor client to generate.
/// </summary>
internal record class ActorClientDescriptor : IEquatable<ActorClientDescriptor>
{
    /// <summary>
    /// Gets or sets the symbol representing the actor interface.
    /// </summary>
    public INamedTypeSymbol InterfaceType { get; set; } = null!;

    /// <summary>
    /// Accessibility of the generated client.
    /// </summary>
    public Accessibility Accessibility { get; set; }

    /// <summary>
    /// Namespace of the generated client.
    /// </summary>
    public string NamespaceName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the generated client.
    /// </summary>
    public string ClientTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified type name of the generated client.
    /// </summary>
    public string FullyQualifiedTypeName => $"{NamespaceName}.{ClientTypeName}";

    /// <summary>
    /// Methods to generate in the client.
    /// </summary>
    public ImmutableArray<IMethodSymbol> Methods { get; set; } = Array.Empty<IMethodSymbol>().ToImmutableArray();

    /// <summary>
    /// Compilation to use for generating the client.
    /// </summary>
    public Compilation Compilation { get; set; } = null!;
}