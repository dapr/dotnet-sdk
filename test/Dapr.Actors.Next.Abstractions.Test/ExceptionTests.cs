using Dapr.Actors.Next.Abstractions.Exceptions;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class ExceptionTests
{
    [Fact]
    public void BaseException_ConstructorsSetExpectedProperties()
    {
        var inner = new InvalidOperationException("inner");

        Assert.IsType<DaprActorException>(new DaprActorException());
        Assert.Equal("message", new DaprActorException("message").Message);
        Assert.Same(inner, new DaprActorException("message", inner).InnerException);
    }

    [Fact]
    public void DerivedException_ConstructorsSetExpectedProperties()
    {
        var inner = new InvalidOperationException("inner");

        AssertDerived(new ActorActivationException(), new ActorActivationException("message"), new ActorActivationException("message", inner), inner);
        AssertDerived(new ActorInvocationException(), new ActorInvocationException("message"), new ActorInvocationException("message", inner), inner);
        AssertDerived(new ActorStateException(), new ActorStateException("message"), new ActorStateException("message", inner), inner);
        AssertDerived(new InvalidActorEventException(), new InvalidActorEventException("message"), new InvalidActorEventException("message", inner), inner);
    }

    [Fact]
    public void InvalidActorEventException_StateEventConstructorCapturesNames()
    {
        var ex = new InvalidActorEventException(TestState.Open, new TestEvent());

        Assert.Contains("Open", ex.Message);
        Assert.Contains(nameof(TestEvent), ex.Message);
        Assert.Equal("Open", ex.StateName);
        Assert.Equal(typeof(TestEvent).FullName, ex.EventName);
    }

    [Fact]
    public void InvalidActorEventException_StateEventConstructorAllowsNulls()
    {
        var ex = new InvalidActorEventException((object?)null, (object?)null);

        Assert.Contains("''", ex.Message);
        Assert.Null(ex.StateName);
        Assert.Null(ex.EventName);
    }

    private static void AssertDerived(DaprActorException empty, DaprActorException withMessage, DaprActorException withInner, Exception inner)
    {
        Assert.NotNull(empty);
        Assert.Equal("message", withMessage.Message);
        Assert.Same(inner, withInner.InnerException);
    }

    private enum TestState
    {
        Open,
    }

    private sealed class TestEvent
    {
    }
}
