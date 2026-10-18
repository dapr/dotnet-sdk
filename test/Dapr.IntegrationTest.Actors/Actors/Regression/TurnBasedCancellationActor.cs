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

using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.Regression;

/// <summary>
/// Uses out-of-band signals so inspecting or releasing a turn does not itself require an actor turn.
/// </summary>
public sealed class TurnBasedCancellationProbe
{
    public bool CooperateWithCancellation { get; init; }
    public TaskCompletionSource<Actor> FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource FirstFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource HostCancellationObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<(Actor Instance, bool FirstFinished)> SecondEntered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// Continues executing after cancellation, as actor methods without cooperative cancellation can do.
/// </summary>
public sealed class TurnBasedCancellationActor(ActorHost host, TurnBasedCancellationProbe probe)
    : Actor(host), ITurnBasedCancellationActor
{
    public Task Ping(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task HoldTurn(CancellationToken cancellationToken = default)
    {
        using var registration = cancellationToken.Register(() => probe.HostCancellationObserved.TrySetResult());
        probe.FirstEntered.TrySetResult(this);
        try
        {
            // Caller cancellation must not be mistaken for completion of this still-running method.
            await probe.ReleaseFirst.Task.WaitAsync(probe.CooperateWithCancellation ? cancellationToken : CancellationToken.None);
        }
        finally
        {
            // Cooperative cancellation can also leave asynchronous cleanup running.
            await probe.ReleaseFirst.Task;
            probe.FirstFinished.TrySetResult();
        }
    }

    public Task ObserveTurn(CancellationToken cancellationToken = default)
    {
        probe.SecondEntered.TrySetResult((this, probe.FirstFinished.Task.IsCompleted));
        return Task.CompletedTask;
    }
}
