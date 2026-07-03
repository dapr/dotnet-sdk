using System.Reflection;
using Dapr.Actors.Next.Abstractions.Attributes;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class AttributeTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void DaprActorAttribute_DefaultsContractVersion()
    {
        var attribute = new DaprActorAttribute();

        Assert.Null(attribute.ActorType);
        Assert.Equal(1, attribute.ContractVersion);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void DaprActorAttribute_StoresActorTypeAndContractVersion()
    {
        var attribute = new DaprActorAttribute("CartActor")
        {
            ContractVersion = 3,
        };

        Assert.Equal("CartActor", attribute.ActorType);
        Assert.Equal(3, attribute.ContractVersion);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AttributeUsage_MatchesPublicContract()
    {
        AssertUsage<DaprActorAttribute>(AttributeTargets.Class, allowMultiple: false);
        AssertUsage<GenerateActorClientAttribute>(AttributeTargets.Interface, allowMultiple: false);
        AssertUsage<SubscribeAttribute>(AttributeTargets.Method, allowMultiple: true);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void SubscribeAttribute_StoresSubscriptionShape()
    {
        var attribute = new SubscribeAttribute("pubsub", "topic")
        {
            RouteBy = "subject",
        };

        Assert.Equal("pubsub", attribute.PubsubName);
        Assert.Equal("topic", attribute.Topic);
        Assert.Equal("subject", attribute.RouteBy);
    }

    private static void AssertUsage<TAttribute>(AttributeTargets targets, bool allowMultiple)
        where TAttribute : Attribute
    {
        var usage = typeof(TAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(targets, usage!.ValidOn);
        Assert.False(usage.Inherited);
        Assert.Equal(allowMultiple, usage.AllowMultiple);
    }
}
