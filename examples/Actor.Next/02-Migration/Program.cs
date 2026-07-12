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
using Dapr.Actors.Next.Examples.Migration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprActors();

var app = builder.Build();

app.MapGet("/", () => "Migration actor sample. POST a legacy cart shape, then GET /carts/{cartId} to read the current shape.");

app.MapGet("/carts/{cartId}", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetState(cancellationToken));

app.MapPost("/carts/{cartId}/legacy/v1", async (
    string cartId,
    CartStateV1 state,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).ImportLegacyV1(state, cancellationToken);
    return Results.Accepted();
});

app.MapPost("/carts/{cartId}/legacy/v2", async (
    string cartId,
    CartStateV2 state,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).ImportLegacyV2(state, cancellationToken);
    return Results.Accepted();
});

app.MapGet("/carts/{cartId}/autonomous", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetAutonomousState(cancellationToken));

app.MapPost("/carts/{cartId}/autonomous/legacy", async (
    string cartId,
    MyState state,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).ImportAutonomousV1(state, cancellationToken);
    return Results.Accepted();
});

app.MapGet("/carts/{cartId}/renamed", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetRenamedState(cancellationToken));

app.MapPost("/carts/{cartId}/renamed/legacy", async (
    string cartId,
    RenamedState state,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).ImportRenamedV1(state, cancellationToken);
    return Results.Accepted();
});

app.MapGet("/carts/{cartId}/graduated", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetGraduatedState(cancellationToken));

app.MapPost("/carts/{cartId}/graduated", async (
    string cartId,
    GraduatedCartState state,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).ImportGraduated(state, cancellationToken);
    return Results.Accepted();
});

app.MapPost("/carts/{cartId}/graduated/offramp", async (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateCart(proxies, cartId).GraduateCart(cancellationToken);
    return Results.Accepted();
});

app.MapGet("/carts/{cartId}/graduated/reimported", (
    string cartId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateCart(proxies, cartId).GetReimportedGraduatedState(cancellationToken));

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
