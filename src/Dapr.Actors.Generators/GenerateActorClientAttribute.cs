namespace Dapr.Actors;

/// <summary>
/// Indicates an actor interface should have a strongly-typed client generated for it.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateActorClientAttribute : Attribute
{
}
