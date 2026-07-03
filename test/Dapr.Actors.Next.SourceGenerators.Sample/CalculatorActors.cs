using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.SourceGenerators.Sample;

[GenerateActorClient]
public interface ICalculatorActor : IActor
{
    Task AddAsync(CalculationInput input, CancellationToken cancellationToken = default);

    Task<int> SumAsync(int left, int right, CancellationToken cancellationToken = default);

    ValueTask<CalculationResult> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record CalculationInput(int Value);

public sealed record CalculationResult(int Value, IReadOnlyList<string> Tags);

public sealed class CalculatorDependency
{
    public int Bias => 1;
}

[DaprActor("Calculator", ContractVersion = 2)]
public sealed class CalculatorActor : Actor, ICalculatorActor
{
    private readonly ActorActivationContext context;
    private readonly CalculatorDependency dependency;

    public CalculatorActor(ActorActivationContext context, CalculatorDependency dependency)
    {
        this.context = context;
        this.dependency = dependency;
    }

    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task AddAsync(CalculationInput input, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("calculator", () => new CalculatorState(), cancellationToken);
        state.Value.Value += input.Value + dependency.Bias;
    }

    public Task<int> SumAsync(int left, int right, CancellationToken cancellationToken = default) =>
        Task.FromResult(left + right + dependency.Bias);

    public async ValueTask<CalculationResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("calculator", () => new CalculatorState(), cancellationToken);
        return new CalculationResult(state.Value.Value, ["generated", Id.Value]);
    }
}

public sealed class CalculatorState
{
    public int Value { get; set; }
}

public sealed class CalculatorStateV1
{
    public int Value { get; set; }
}

public sealed class CalculatorStateV2
{
    public int Value { get; set; }
}

public sealed class CalculatorStateUpcaster : IActorStateUpcaster<CalculatorStateV1, CalculatorStateV2>
{
    public ValueTask<CalculatorStateV2> UpcastAsync(CalculatorStateV1 state, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new CalculatorStateV2 { Value = state.Value });
}
