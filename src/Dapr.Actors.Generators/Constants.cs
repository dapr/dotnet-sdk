namespace Dapr.Actors.Generators;

/// <summary>
/// Constants used by the code generator.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The namespace used by the generated code.
    /// </summary>
    public const string GeneratorsNamespace = "Dapr.Actors.Generators";

    /// <summary>
    /// The name of the attribute used to mark actor interfaces.
    /// </summary>
    public const string ActorMethodAttributeTypeName = "ActorMethodAttribute";

    /// <summary>
    /// The full type name of the attribute used to mark actor interfaces.
    /// </summary>
    public const string ActorMethodAttributeFullTypeName = GeneratorsNamespace + "." + ActorMethodAttributeTypeName;

    /// <summary>
    /// The name of the attribute used to mark actor interfaces.
    /// </summary>
    public const string GenerateActorClientAttributeTypeName = "GenerateActorClientAttribute";

    /// <summary>
    /// The full type name of the attribute used to mark actor interfaces.
    /// </summary>
    public const string GenerateActorClientAttributeFullTypeName = GeneratorsNamespace + "." + GenerateActorClientAttributeTypeName;

    /// <summary>
    /// Actor proxy type name.
    /// </summary>
    public const string ActorProxyTypeName = "Dapr.Actors.Client.ActorProxy";
}