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
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.Examples.Timers;

[GenerateActorClient]
public interface ITimerDemoActor : IActor
{
    /// <summary>Creates or replaces a named timer that calls <see cref="Tick"/>.</summary>
    Task Schedule(ScheduleTimerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one timer back from the hosting sidecar; <see cref="TimerLookup.Timer"/> is null when it is not registered.</summary>
    Task<TimerLookup> Get(string name, CancellationToken cancellationToken = default);

    /// <summary>Lists every timer registered for this actor on the hosting sidecar.</summary>
    Task<TimerView[]> List(CancellationToken cancellationToken = default);

    /// <summary>Cancels a named timer.</summary>
    Task Cancel(string name, CancellationToken cancellationToken = default);

    /// <summary>Returns the messages delivered by fired timers, oldest first.</summary>
    Task<string[]> GetFired(CancellationToken cancellationToken = default);

    /// <summary>Timer callback.</summary>
    Task Tick(TickPayload payload, CancellationToken cancellationToken = default);
}

public sealed record ScheduleTimerRequest(string Name, int DueSeconds, string Message, int? PeriodSeconds = null, int? TtlSeconds = null);

public sealed record TickPayload(string TimerName, string Message);

public sealed record TimerView(string Name, string? OperationName, TimeSpan? DueTime, TimeSpan? Period, TimeSpan? Ttl, string? ArgumentsJson);

// Generated actor clients do not support nullable reference return types, so the lookup result is wrapped.
public sealed record TimerLookup(TimerView? Timer);

public sealed class FiredState
{
    public List<string> Entries { get; set; } = [];
}

/// <summary>
/// Exercises the full timer API of <see cref="IActorTimerScheduler"/>: schedule, get, list and cancel.
/// Get and List are host-local and need Dapr runtime 1.19 or later, so they are called from inside the actor.
/// </summary>
[DaprActor(ActorType)]
public sealed class TimerDemoActor(ActorActivationContext context, IActorTimerScheduler timers) : Actor, ITimerDemoActor
{
    public const string ActorType = "TimerDemo";

    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public Task Schedule(ScheduleTimerRequest request, CancellationToken cancellationToken = default) =>
        timers.RescheduleAsync(
            ActorType,
            Id,
            request.Name,
            TimeSpan.FromSeconds(request.DueSeconds),
            nameof(Tick),
            new TickPayload(request.Name, request.Message),
            period: request.PeriodSeconds is { } period ? TimeSpan.FromSeconds(period) : null,
            ttl: request.TtlSeconds is { } ttl ? TimeSpan.FromSeconds(ttl) : null,
            cancellationToken: cancellationToken).AsTask();

    public async Task<TimerLookup> Get(string name, CancellationToken cancellationToken = default)
    {
        var timer = await timers.GetAsync(ActorType, Id, name, cancellationToken);
        return new TimerLookup(timer is null ? null : ToView(name, timer));
    }

    public async Task<TimerView[]> List(CancellationToken cancellationToken = default)
    {
        var registered = await timers.ListAsync(ActorType, Id, cancellationToken);
        return registered.Select(timer => ToView(timer.Name, timer.Timer)).ToArray();
    }

    public Task Cancel(string name, CancellationToken cancellationToken = default) =>
        timers.CancelAsync(ActorType, Id, name, cancellationToken).AsTask();

    public async Task<string[]> GetFired(CancellationToken cancellationToken = default)
    {
        var fired = await State.GetOrCreateAsync("fired", () => new FiredState(), cancellationToken);
        return fired.Value.Entries.ToArray();
    }

    public async Task Tick(TickPayload payload, CancellationToken cancellationToken = default)
    {
        var fired = await State.GetOrCreateAsync("fired", () => new FiredState(), cancellationToken);
        fired.Value.Entries.Add($"{payload.TimerName}: {payload.Message}");
    }

    private static TimerView ToView(string name, ActorTimerInfo timer) =>
        new(name, timer.OperationName, timer.DueTime, timer.Period, timer.Ttl, timer.ArgumentsJson);
}
