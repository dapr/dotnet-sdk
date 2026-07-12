// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Reflection;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Examples.PubSub;
using Dapr.Actors.Next.Streams;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Examples.PubSub.Tests;

public sealed class PubSubTests
{
    private static readonly ActorStreamSubscription Subscription =
        new("orders-pubsub", "inventory-restocked", "RestockingCart", nameof(IRestockingCartActor.OnRestock), nameof(RestockEvent.CartId));
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Publishing_one_event_wakes_only_the_named_cart()
    {
        await using var runtime = CreateRuntime();
        var named = runtime.CreateActor<IRestockingCartActor>(ActorId.Create("cart-1"), "RestockingCart");
        var other = runtime.CreateActor<IRestockingCartActor>(ActorId.Create("cart-2"), "RestockingCart");
        await AddSku(runtime, named, "sku-1");
        await AddSku(runtime, other, "sku-1");

        var delivery = Runner(runtime).ProcessEventAsync(Subscription, Event(new RestockEvent("cart-1", "sku-1")));
        await runtime.RunToIdle();
        var action = await delivery;

        Assert.Equal(ActorStreamDeliveryAction.Ack, action);
        Assert.True(await IsAvailable(runtime, named, "sku-1"));
        Assert.False(await IsAvailable(runtime, other, "sku-1"));
    }

    [Fact]
    public async Task Transient_state_write_fault_retries_delivery_instead_of_acknowledging()
    {
        await using var runtime = CreateRuntime();
        var cart = runtime.CreateActor<IRestockingCartActor>(ActorId.Create("cart-retry"), "RestockingCart");
        await AddSku(runtime, cart, "sku-1");

        runtime.Faults.FailNextStateWrite<RestockingCartState>();
        var firstDelivery = Runner(runtime).ProcessEventAsync(Subscription, Event(new RestockEvent("cart-retry", "sku-1")));
        await runtime.RunToIdle();
        var first = await firstDelivery;
        var secondDelivery = Runner(runtime).ProcessEventAsync(Subscription, Event(new RestockEvent("cart-retry", "sku-1")));
        await runtime.RunToIdle();
        var second = await secondDelivery;

        Assert.Equal(ActorStreamDeliveryAction.Retry, first);
        Assert.Equal(ActorStreamDeliveryAction.Ack, second);
        Assert.True(await IsAvailable(runtime, cart, "sku-1"));
    }

    private static ActorTestRuntime CreateRuntime()
    {
        _ = typeof(RestockingCartActor);
        return new ActorTestRuntime(services => services.AddDaprActors(_ => { }));
    }

    private static ActorStreamSubscriptionRunner Runner(ActorTestRuntime runtime)
    {
        var invocationClient = (IActorInvocationClient)runtime.GetType().GetProperty("Runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        return new ActorStreamSubscriptionRunner(
            new ActorStreamForwarder(invocationClient, new ActorStreamRoutingKeyExtractor()),
            new DefaultActorStreamFailureClassifier());
    }

    private static ActorStreamEvent Event(RestockEvent evt) =>
        new("event-1", "orders-pubsub", "inventory-restocked", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, WebJsonOptions)), new Dictionary<string, string>());

    private static async Task AddSku(ActorTestRuntime runtime, IRestockingCartActor cart, string sku)
    {
        var add = cart.AddUnavailableSku(sku);
        await runtime.RunToIdle();
        await add;
    }

    private static async Task<bool> IsAvailable(ActorTestRuntime runtime, IRestockingCartActor cart, string sku)
    {
        var read = cart.IsAvailable(sku);
        await runtime.RunToIdle();
        return await read;
    }
}
