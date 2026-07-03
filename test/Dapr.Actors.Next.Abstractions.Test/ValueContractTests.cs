using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Scheduling;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class ValueContractTests
{
    [Fact]
    public void DispatchRecords_UseRecordEquality()
    {
        var actorId = ActorId.Create("1");
        var requestContext = new ActorRequestContext("trace", "state", new Dictionary<string, string> { ["tenant"] = "a" });
        var headers = new Dictionary<string, string> { ["h"] = "v" };
        var payload = new byte[] { (byte)'[', (byte)']' };
        var result = new byte[] { (byte)'{', (byte)'}' };
        var request = new ActorDispatchRequest("CartActor", actorId, "Add", payload, headers, requestContext);
        var response = new ActorDispatchResponse(result);

        Assert.Equal(request, new ActorDispatchRequest("CartActor", actorId, "Add", payload, headers, requestContext));
        Assert.Equal(response, new ActorDispatchResponse(result));
        Assert.NotEqual(response, new ActorDispatchResponse(null));
    }

    [Fact]
    public void TurnRecords_UseRecordEquality()
    {
        var actorId = ActorId.Create("1");
        var requestContext = new ActorRequestContext("trace", "state", new Dictionary<string, string>());
        var headers = new Dictionary<string, string>();
        var turn = new ActorTurn("CartActor", actorId, "Add", ActorTurnKind.Invoke, requestContext, headers);
        var context = new ActorTurnContext("CartActor", actorId, "Add", ActorTurnKind.Invoke, headers, requestContext, CancellationToken.None);

        Assert.Equal(requestContext, new ActorRequestContext("trace", "state", requestContext.Baggage));
        Assert.Equal(turn, new ActorTurn("CartActor", actorId, "Add", ActorTurnKind.Invoke, requestContext, headers));
        Assert.Equal(context, new ActorTurnContext("CartActor", actorId, "Add", ActorTurnKind.Invoke, headers, requestContext, CancellationToken.None));
    }

    [Fact]
    public void CapabilityContext_UsesRecordEquality()
    {
        var actorId = ActorId.Create("1");
        var requestContext = new ActorRequestContext(null, null, new Dictionary<string, string>());
        var args = new Dictionary<string, object?> { ["amount"] = 10m };
        var context = new ActorCapabilityContext("AuctionActor", actorId, "accept-bid", args, requestContext);

        Assert.Equal(context, new ActorCapabilityContext("AuctionActor", actorId, "accept-bid", args, requestContext));
        Assert.NotEqual(context, context with { CapabilityName = "reject-bid" });
    }
}
