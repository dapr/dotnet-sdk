using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class DescriptorTests
{
    [Fact]
    public void ActorDescriptors_UseRecordEquality()
    {
        var parameters = new[]
        {
            new ActorParameterDescriptor("item", typeof(string), 0, false, false, null),
            new ActorParameterDescriptor("ct", typeof(CancellationToken), 1, true, true, default(CancellationToken)),
        };
        var methods = new[]
        {
            new ActorMethodDescriptor("AddItem", "AddItem", typeof(Task), parameters),
        };

        var left = new ActorTypeDescriptor("CartActor", 1, typeof(TestActor), typeof(ITestActor), methods);
        var right = new ActorTypeDescriptor("CartActor", 1, typeof(TestActor), typeof(ITestActor), methods);
        var different = right with { ContractVersion = 2 };

        Assert.Equal(left, right);
        Assert.NotEqual(left, different);
        Assert.Equal(methods[0], new ActorMethodDescriptor("AddItem", "AddItem", typeof(Task), parameters));
        Assert.Equal(parameters[0], new ActorParameterDescriptor("item", typeof(string), 0, false, false, null));
    }

    private interface ITestActor : IActor
    {
    }

    private sealed class TestActor : Actor
    {
        protected override ActorId Id => ActorId.Create("test");

        protected override IActorStateAccessor State => throw new NotSupportedException();
    }
}
