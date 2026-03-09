using Dapr.Testcontainers.Harnesses;
using Xunit;

namespace Dapr.Testcontainers.Test.Harnesses;

public sealed class DaprTestEnvironmentTests
{
    [Fact]
    public void Constructor_ShouldExposeRedisContainer_WhenNeedsActorStateIsTrue()
    {
        var env = new DaprTestEnvironment(needsActorState: true);
        Assert.NotNull(env.RedisContainer);
    }

    [Fact]
    public void Constructor_ShouldNotExposeRedisContainer_WhenNeedsActorStateIsFalse()
    {
        var env = new DaprTestEnvironment(needsActorState: false);
        Assert.Null(env.RedisContainer);
    }
}
