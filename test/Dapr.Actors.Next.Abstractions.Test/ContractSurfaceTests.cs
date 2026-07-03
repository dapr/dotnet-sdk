using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class ContractSurfaceTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void ActorLifecycleHooks_AreVirtualNoOps()
    {
        var actor = new TestActor();
        var context = new ActorMethodContext("TestActor", ActorId.Create("1"), "Run", [], new Dictionary<string, string>());

        Assert.True(actor.Activate().IsCompletedSuccessfully);
        Assert.True(actor.Deactivate().IsCompletedSuccessfully);
        Assert.True(actor.Pre(context).IsCompletedSuccessfully);
        Assert.True(actor.Post(context).IsCompletedSuccessfully);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Upcaster_GenericParameterOrderIsFromThenTo()
    {
        var parameters = typeof(IActorStateUpcaster<,>).GetGenericArguments();

        Assert.Equal("TFromType", parameters[0].Name);
        Assert.Equal("TToType", parameters[1].Name);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void DispatcherContract_DoesNotExposeTypeBasedSerializerInputs()
    {
        var requestProperties = typeof(ActorDispatchRequest).GetProperties();
        var method = typeof(IActorDispatcher).GetMethod(nameof(IActorDispatcher.DispatchAsync));

        Assert.DoesNotContain(requestProperties, property => property.PropertyType == typeof(Type));
        Assert.NotNull(method);
        Assert.DoesNotContain(method!.GetParameters(), parameter => parameter.ParameterType == typeof(Type));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void StateEnvelope_UsesRecordValueEquality()
    {
        Assert.Equal(new ActorStateEnvelope<string>(1, "value"), new ActorStateEnvelope<string>(1, "value"));
        Assert.NotEqual(new ActorStateEnvelope<string>(1, "value"), new ActorStateEnvelope<string>(2, "value"));
    }

    private sealed class TestActor : Actor
    {
        protected override ActorId Id => ActorId.Create("1");

        protected override IActorStateAccessor State => throw new NotSupportedException();

        public ValueTask Activate() => OnActivateAsync();

        public ValueTask Deactivate() => OnDeactivateAsync();

        public ValueTask Pre(ActorMethodContext context) => OnPreActorMethodAsync(context);

        public ValueTask Post(ActorMethodContext context) => OnPostActorMethodAsync(context, null);
    }
}
