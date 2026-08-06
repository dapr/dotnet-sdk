using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using LegacyActor = Dapr.Actors.Runtime.Actor;
using LegacyActorHost = Dapr.Actors.Runtime.ActorHost;
using LegacyIActor = Dapr.Actors.IActor;
using NextActor = Dapr.Actors.Next.Abstractions.Actor;
using NextActorId = Dapr.Actors.Next.Abstractions.ActorId;
using NextIActor = Dapr.Actors.Next.Abstractions.IActor;

namespace Dapr.Actors.Benchmarks;

public interface ILegacyBenchmarkActor : LegacyIActor
{
    Task<int> AddAsync(int value);
}

public interface INextBenchmarkActor : NextIActor
{
    Task<int> AddAsync(int value, CancellationToken cancellationToken = default);
}

[GenerateActorClient]
public interface INextBenchmarkActor01 : INextBenchmarkActor;

[GenerateActorClient]
public interface INextBenchmarkActor02 : INextBenchmarkActor;

[GenerateActorClient]
public interface INextBenchmarkActor03 : INextBenchmarkActor;

[GenerateActorClient]
public interface INextBenchmarkActor04 : INextBenchmarkActor;

[GenerateActorClient]
public interface INextBenchmarkActor05 : INextBenchmarkActor;

[GenerateActorClient]
public interface INextBenchmarkActor06 : INextBenchmarkActor;

[GenerateActorClient]
public interface INextBenchmarkActor07 : INextBenchmarkActor;

[GenerateActorClient]
public interface INextBenchmarkActor08 : INextBenchmarkActor;

public abstract class LegacyBenchmarkActor(LegacyActorHost host) : LegacyActor(host), ILegacyBenchmarkActor
{
    private int value;

    public Task<int> AddAsync(int value)
    {
        this.value += value;
        return Task.FromResult(this.value);
    }
}

public abstract class NextBenchmarkActor(ActorActivationContext context) : NextActor
{
    private int value;

    protected override NextActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public Task<int> AddAsync(int value, CancellationToken cancellationToken = default)
    {
        this.value += value;
        return Task.FromResult(this.value);
    }
}

public sealed class LegacyBenchmarkActor01(LegacyActorHost host) : LegacyBenchmarkActor(host);
public sealed class LegacyBenchmarkActor02(LegacyActorHost host) : LegacyBenchmarkActor(host);
public sealed class LegacyBenchmarkActor03(LegacyActorHost host) : LegacyBenchmarkActor(host);
public sealed class LegacyBenchmarkActor04(LegacyActorHost host) : LegacyBenchmarkActor(host);
public sealed class LegacyBenchmarkActor05(LegacyActorHost host) : LegacyBenchmarkActor(host);
public sealed class LegacyBenchmarkActor06(LegacyActorHost host) : LegacyBenchmarkActor(host);
public sealed class LegacyBenchmarkActor07(LegacyActorHost host) : LegacyBenchmarkActor(host);
public sealed class LegacyBenchmarkActor08(LegacyActorHost host) : LegacyBenchmarkActor(host);

[DaprActor("BenchmarkActor01")]
public sealed class NextBenchmarkActor01(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor01;

[DaprActor("BenchmarkActor02")]
public sealed class NextBenchmarkActor02(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor02;

[DaprActor("BenchmarkActor03")]
public sealed class NextBenchmarkActor03(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor03;

[DaprActor("BenchmarkActor04")]
public sealed class NextBenchmarkActor04(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor04;

[DaprActor("BenchmarkActor05")]
public sealed class NextBenchmarkActor05(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor05;

[DaprActor("BenchmarkActor06")]
public sealed class NextBenchmarkActor06(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor06;

[DaprActor("BenchmarkActor07")]
public sealed class NextBenchmarkActor07(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor07;

[DaprActor("BenchmarkActor08")]
public sealed class NextBenchmarkActor08(ActorActivationContext context) : NextBenchmarkActor(context), INextBenchmarkActor08;
