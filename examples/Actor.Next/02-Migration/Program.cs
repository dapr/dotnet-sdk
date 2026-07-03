using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Examples.Migration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprActors();

var app = builder.Build();

app.MapGet("/", () => "Migration actor sample. POST /carts/{cartId}/legacy/v1 or /legacy/v2, then GET /carts/{cartId}.");

app.MapGet("/carts/{cartId}", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetCurrentState(cancellationToken));

app.MapPost("/carts/{cartId}/legacy/v1", async (
    string cartId,
    CartStateV1 state,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => Results.Ok(await CreateCart(proxies, cartId).ImportLegacyV1(state, cancellationToken)));

app.MapPost("/carts/{cartId}/legacy/v2", async (
    string cartId,
    CartStateV2 state,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => Results.Ok(await CreateCart(proxies, cartId).ImportLegacyV2(state, cancellationToken)));

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

static IMigratingCartActor CreateCart(IActorProxyFactory proxies, string cartId) =>
    proxies.Create<IMigratingCartActor>(ActorId.Create(cartId), "MigratingCart");
