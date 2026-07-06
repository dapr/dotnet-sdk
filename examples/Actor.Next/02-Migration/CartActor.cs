using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.Examples.Migration;

[GenerateActorClient]
public interface IMigratingCartActor : IActor
{
    Task<CartStateV3> GetState(CancellationToken cancellationToken = default);

    Task<CartStateV3> TryGetState(CancellationToken cancellationToken = default);

    Task ImportLegacyV1(CartStateV1 state, CancellationToken cancellationToken = default);

    Task ImportLegacyV2(CartStateV2 state, CancellationToken cancellationToken = default);

    Task AddSku(string sku, CancellationToken cancellationToken = default);

    Task<MyStateV3> GetAutonomousState(CancellationToken cancellationToken = default);

    Task ImportAutonomousV1(MyState state, CancellationToken cancellationToken = default);

    Task<RenamedStateV2> GetRenamedState(CancellationToken cancellationToken = default);

    Task ImportRenamedV1(RenamedState state, CancellationToken cancellationToken = default);

    Task<GraduatedCartState> GetGraduatedState(CancellationToken cancellationToken = default);

    Task<GraduatedCartStateV2> GetReimportedGraduatedState(CancellationToken cancellationToken = default);

    Task GraduateCart(CancellationToken cancellationToken = default);

    Task ImportGraduated(GraduatedCartState state, CancellationToken cancellationToken = default);

    Task Clear(CancellationToken cancellationToken = default);
}

[DaprActor("MigratingCart")]
public sealed class MigratingCartActor(ActorActivationContext context) : Actor, IMigratingCartActor
{
    private const string CartStateName = "cart";
    private const string AutonomousStateName = "autonomous";
    private const string RenamedStateName = "renamed";
    private const string GraduatedStateName = "graduated";

    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task<CartStateV3> GetState(CancellationToken cancellationToken = default) =>
        (await State.GetOrCreateAsync(CartStateName, static () => new CartStateV3(), cancellationToken)).Value;

    public async Task<CartStateV3> TryGetState(CancellationToken cancellationToken = default) =>
        (await State.TryGetAsync<CartStateV3>(CartStateName, cancellationToken))?.Value ?? new CartStateV3();

    public Task ImportLegacyV1(CartStateV1 state, CancellationToken cancellationToken = default) =>
        State.SetAsync(CartStateName, state, cancellationToken).AsTask();

    public Task ImportLegacyV2(CartStateV2 state, CancellationToken cancellationToken = default) =>
        State.SetAsync(CartStateName, state, cancellationToken).AsTask();

    public async Task AddSku(string sku, CancellationToken cancellationToken = default)
    {
        var cart = await State.GetOrCreateAsync(CartStateName, static () => new CartStateV3(), cancellationToken);
        var lines = cart.Value.Lines.ToList();
        var existing = lines.FindIndex(line => string.Equals(line.Sku, sku, StringComparison.Ordinal));
        if (existing >= 0)
        {
            var line = lines[existing];
            lines[existing] = line with { Quantity = line.Quantity + 1 };
        }
        else
        {
            lines.Add(new CartLine(sku, 1));
        }

        cart.Value = new CartStateV3
        {
            Lines = lines,
            TotalQuantity = lines.Sum(line => line.Quantity),
        };
    }

    public async Task<MyStateV3> GetAutonomousState(CancellationToken cancellationToken = default) =>
        (await State.GetOrCreateAsync(
            AutonomousStateName,
            static () => new MyStateV3 { Name = "default", Active = true },
            cancellationToken)).Value;

    public Task ImportAutonomousV1(MyState state, CancellationToken cancellationToken = default) =>
        State.SetAsync(AutonomousStateName, state, cancellationToken).AsTask();

    public async Task<RenamedStateV2> GetRenamedState(CancellationToken cancellationToken = default) =>
        (await State.GetOrCreateAsync(RenamedStateName, static () => new RenamedStateV2(), cancellationToken)).Value;

    public Task ImportRenamedV1(RenamedState state, CancellationToken cancellationToken = default) =>
        State.SetAsync(RenamedStateName, state, cancellationToken).AsTask();

    public async Task<GraduatedCartState> GetGraduatedState(CancellationToken cancellationToken = default) =>
        (await State.GetOrCreateAsync(GraduatedStateName, static () => new GraduatedCartState(), cancellationToken)).Value;

    public async Task<GraduatedCartStateV2> GetReimportedGraduatedState(CancellationToken cancellationToken = default) =>
        (await State.GetOrCreateAsync(GraduatedStateName, static () => new GraduatedCartStateV2(), cancellationToken)).Value;

    public Task GraduateCart(CancellationToken cancellationToken = default) =>
        State.GraduateAsync<GraduatedCartState>(GraduatedStateName, cancellationToken).AsTask();

    public Task ImportGraduated(GraduatedCartState state, CancellationToken cancellationToken = default) =>
        State.SetAsync(GraduatedStateName, state, cancellationToken).AsTask();

    public async Task Clear(CancellationToken cancellationToken = default)
    {
        await State.RemoveAsync(CartStateName, cancellationToken);
        await State.RemoveAsync(AutonomousStateName, cancellationToken);
        await State.RemoveAsync(RenamedStateName, cancellationToken);
        await State.RemoveAsync(GraduatedStateName, cancellationToken);
    }
}
