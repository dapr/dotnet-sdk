namespace Dapr.Actors.Next.Abstractions.Attributes;

/// <summary>
/// Requests generated client proxy and dispatcher code for an actor interface.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateActorClientAttribute : Attribute
{
}
