using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Examples.Cart;
using Dapr.Common.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPricingClient, StaticPricingClient>();
builder.Services.AddSingleton<IDaprSerializer, AotCartDaprSerializer>();
builder.Services.AddSingleton<IActorWireSerializer, AotCartWireSerializer>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AotCartJsonContext.Default));
builder.Services.AddDaprActors();

var app = builder.Build();

app.MapGet("/", () => "Cart actor sample. POST /carts/{cartId}/items and GET /carts/{cartId}/summary.");

app.MapPost("/carts/{cartId}/items", async (
    string cartId,
    CartItem item,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).AddItem(item, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/carts/{cartId}/summary", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetSummary(cancellationToken));

app.MapPost("/carts/{cartId}/abandon", async (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).AbandonCart(cancellationToken);
    return Results.NoContent();
});

await app.RunAsync();
return;

static ICartActor CreateCart(IActorProxyFactory proxies, string cartId) =>
    proxies.Create<ICartActor>(ActorId.Create(cartId), "Cart");

internal sealed class StaticPricingClient : IPricingClient
{
    public ValueTask<decimal> GetPriceAsync(string sku, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(sku == "sku-1" ? 12.50m : 1.00m);
}
