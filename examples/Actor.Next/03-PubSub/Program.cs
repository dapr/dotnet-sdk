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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Examples.PubSub;
using Dapr.Actors.Next.Streams;
using Dapr.Client;
using Dapr.Messaging.PublishSubscribe.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprActors();
builder.Services.AddDaprActorStreams();
builder.Services.AddDaprPubSubClient();
builder.Services.AddSingleton(_ => new DaprClientBuilder().Build());

var app = builder.Build();

// This sample registers the same subscription declared by [Subscribe] on
// RestockingCartActor.OnRestock so the hosted stream service opens it at startup.
app.Services.GetRequiredService<ActorStreamSubscriptionRegistry>().Add(
    new ActorStreamSubscription(
        RestockingCartNames.PubsubName,
        RestockingCartNames.RestockTopic,
        RestockingCartNames.ActorType,
        nameof(IRestockingCartActor.OnRestock),
        nameof(RestockEvent.CartId)));

app.MapGet("/", () => "Pub/sub actor sample. POST /carts/{cartId}/unavailable, POST /inventory/restocked, then GET /carts/{cartId}.");

app.MapPost("/carts/{cartId}/unavailable", async (
    string cartId,
    SkuRequest request,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).AddUnavailableSku(request.Sku, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/carts/{cartId}", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetState(cancellationToken));

app.MapGet("/carts/{cartId}/items/{sku}/availability", async (
    string cartId,
    string sku,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    var available = await CreateCart(proxies, cartId).IsAvailable(sku, cancellationToken);
    return new AvailabilityResponse(cartId, sku, available);
});

app.MapPost("/inventory/restocked", async (
    RestockEvent evt,
    DaprClient dapr,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await dapr.PublishEventAsync(RestockingCartNames.PubsubName, RestockingCartNames.RestockTopic, evt, cancellationToken);
    var available = await WaitForAvailability(CreateCart(proxies, evt.CartId), evt.Sku, cancellationToken);
    return Results.Ok(new RestockPublishResult(evt.CartId, evt.Sku, available));
});

app.MapDelete("/carts/{cartId}", async (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).Clear(cancellationToken);
    return Results.NoContent();
});

await app.RunAsync();
return;

static IRestockingCartActor CreateCart(IActorProxyFactory proxies, string cartId) =>
    proxies.Create<IRestockingCartActor>(ActorId.Create(cartId), RestockingCartNames.ActorType);

static async Task<bool> WaitForAvailability(
    IRestockingCartActor cart,
    string sku,
    CancellationToken cancellationToken)
{
    var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
    while (DateTimeOffset.UtcNow < deadline)
    {
        if (await cart.IsAvailable(sku, cancellationToken))
        {
            return true;
        }

        await Task.Delay(100, cancellationToken);
    }

    return false;
}

internal sealed record SkuRequest(string Sku);

internal sealed record AvailabilityResponse(string CartId, string Sku, bool Available);

internal sealed record RestockPublishResult(string CartId, string Sku, bool AvailableAfterPublish);
