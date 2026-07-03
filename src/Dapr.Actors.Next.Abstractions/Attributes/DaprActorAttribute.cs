namespace Dapr.Actors.Next.Abstractions.Attributes;

/// <summary>
/// Marks a concrete class as a Dapr actor implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DaprActorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new attribute instance.
    /// </summary>
    public DaprActorAttribute()
    {
    }

    /// <summary>
    /// Initializes a new attribute instance with an explicit actor type name.
    /// </summary>
    public DaprActorAttribute(string actorType)
    {
        ActorType = actorType;
    }

    /// <summary>
    /// Gets the explicit actor type name, when supplied.
    /// </summary>
    public string? ActorType { get; }

    /// <summary>
    /// Gets or sets the actor contract version emitted into the generated registry.
    /// </summary>
    public int ContractVersion { get; set; } = 1;
}
