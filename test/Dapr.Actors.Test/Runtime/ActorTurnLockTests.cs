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

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Dapr.Actors.Runtime;

public sealed class ActorTurnLockTests
{
    [Fact]
    public async Task CancellingOwnerDoesNotReleaseTurn()
    {
        var turnLock = new ActorTurnLock();
        var id = ActorId.CreateRandom();
        using var cancellation = new CancellationTokenSource();
        using var first = await turnLock.AcquireAsync(id, null, cancellation.Token);
        var second = turnLock.AcquireAsync(id, null, TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        Assert.False(second.IsCompleted);
        first.Dispose();

        using var next = await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CancelledWaiterDoesNotReleaseOwnerOrLoseOtherWaiters()
    {
        var turnLock = new ActorTurnLock();
        var id = ActorId.CreateRandom();
        using var first = await turnLock.AcquireAsync(id, null, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var cancelled = turnLock.AcquireAsync(id, null, cancellation.Token);
        var next = turnLock.AcquireAsync(id, null, TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(next.IsCompleted);
        first.Dispose();

        using var second = await next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var third = turnLock.AcquireAsync(id, null, TestContext.Current.CancellationToken);
        Assert.False(third.IsCompleted);
        second.Dispose();
        using var last = await third.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotAcquireTurn()
    {
        var turnLock = new ActorTurnLock();
        var id = ActorId.CreateRandom();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            turnLock.AcquireAsync(id, null, new CancellationToken(true)));
        using var next = await turnLock.AcquireAsync(id, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DifferentActorsDoNotBlockEachOther()
    {
        var turnLock = new ActorTurnLock();
        using var first =
            await turnLock.AcquireAsync(new ActorId("first"), null, TestContext.Current.CancellationToken);
        using var second = await turnLock
            .AcquireAsync(new ActorId("second"), null, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReentrancyWaitsForActivationThenEntersMatchingTurnOnly()
    {
        var turnLock = new ActorTurnLock();
        var id = ActorId.CreateRandom();
        using var first = await turnLock.AcquireAsync(id, "chain", TestContext.Current.CancellationToken);
        var matching = turnLock.AcquireAsync(id, "chain", TestContext.Current.CancellationToken);
        var unrelated = turnLock.AcquireAsync(id, "other-chain", TestContext.Current.CancellationToken);
        Assert.False(matching.IsCompleted);

        first.AllowReentrancy();
        using var nested = await matching.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(unrelated.IsCompleted);
        first.Dispose();
        Assert.False(unrelated.IsCompleted);
        nested.Dispose();
        using var next = await unrelated.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LifecycleRequestCannotReenterEvenWithReentrancyEnabled()
    {
        var turnLock = new ActorTurnLock();
        var id = ActorId.CreateRandom();
        using var first = await turnLock.AcquireAsync(id, "chain", TestContext.Current.CancellationToken);
        first.AllowReentrancy();
        var lifecycle = turnLock.AcquireAsync(id, null, TestContext.Current.CancellationToken);
        Assert.False(lifecycle.IsCompleted);
        first.Dispose();
        using var next = await lifecycle.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RepeatedConcurrentTurnsRemainExclusive()
    {
        var turnLock = new ActorTurnLock();
        var id = ActorId.CreateRandom();
        var active = 0;
        var tasks = new Task[20];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = RunAsync();
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        async Task RunAsync()
        {
            for (var i = 0; i < 20; i++)
            {
                using var turn = await turnLock.AcquireAsync(id, null, TestContext.Current.CancellationToken);
                Assert.Equal(1, Interlocked.Increment(ref active));
                await Task.Yield();
                Assert.Equal(0, Interlocked.Decrement(ref active));
            }
        }
    }
}
