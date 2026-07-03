namespace Dapr.Actors.Next.Examples.PubSub;

public static class RestockingCartNames
{
    public const string ActorType = "RestockingCart";

    public const string PubsubName = "orders-pubsub";

    public const string RestockTopic = "inventory-restocked";
}

public sealed record RestockEvent(string CartId, string Sku);

public sealed class RestockingCartState
{
    public HashSet<string> WaitingForStock { get; set; } = [];

    public HashSet<string> Available { get; set; } = [];
}
