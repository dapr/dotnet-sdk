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

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Dapr.Common.Test;

public sealed class ActorActivationIdGateTests
{
    [Theory]
    [InlineData("1.19.1", true)]
    [InlineData("1.19.1-rc.1", true)]
    [InlineData("1.19.2+build.5", true)]
    [InlineData("1.20.0", true)]
    [InlineData("2.0.0", true)]
    [InlineData(" 1.19.1 ", true)]
    [InlineData("1.19.0", false)]
    [InlineData("1.19.0-rc.2", false)]
    [InlineData("1.18.9", false)]
    [InlineData("0.99.99", false)]
    [InlineData("edge", false)]
    [InlineData("dev", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("1.19", false)]
    [InlineData("1.19.1.0", false)]
    [InlineData("v1.19.1", false)]
    [InlineData("1.x.1", false)]
    [InlineData("-1.19.1", false)]
    public async Task FilterAsync_TrustsOnlyMinimumRuntimeVersion(string? version, bool trusted)
    {
        var gate = new ActorActivationIdGate(_ => Task.FromResult(version));

        var result = await gate.FilterAsync("a1", TestContext.Current.CancellationToken);

        Assert.Equal(trusted ? "a1" : null, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FilterAsync_EmptyId_SkipsLookup(string? activationId)
    {
        var lookups = 0;
        var gate = new ActorActivationIdGate(_ =>
        {
            Interlocked.Increment(ref lookups);
            return Task.FromResult<string?>("1.19.1");
        });

        Assert.Equal(activationId, await gate.FilterAsync(activationId, TestContext.Current.CancellationToken));
        Assert.Equal(0, lookups);
    }

    [Fact]
    public async Task FilterAsync_CachesSuccessfulLookup()
    {
        var lookups = 0;
        var gate = new ActorActivationIdGate(_ =>
        {
            Interlocked.Increment(ref lookups);
            return Task.FromResult<string?>("1.19.0");
        });

        Assert.Null(await gate.FilterAsync("a1", TestContext.Current.CancellationToken));
        Assert.Null(await gate.FilterAsync("a2", TestContext.Current.CancellationToken));
        Assert.Equal(1, lookups);
    }

    [Fact]
    public async Task FilterAsync_FailedLookup_IgnoresIdThenRetries()
    {
        var lookups = 0;
        var gate = new ActorActivationIdGate(_ =>
            Interlocked.Increment(ref lookups) == 1
                ? Task.FromException<string?>(new InvalidOperationException("sidecar down"))
                : Task.FromResult<string?>("1.19.1"));

        Assert.Null(await gate.FilterAsync("a1", TestContext.Current.CancellationToken));
        Assert.Equal("a1", await gate.FilterAsync("a1", TestContext.Current.CancellationToken));
        Assert.Equal("a1", await gate.FilterAsync("a1", TestContext.Current.CancellationToken));
        Assert.Equal(2, lookups);
    }

    [Fact]
    public async Task FilterAsync_LookupTimeout_IgnoresIdThenRetries()
    {
        var lookups = 0;
        var gate = new ActorActivationIdGate(
            async cancellationToken =>
            {
                if (Interlocked.Increment(ref lookups) == 1)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                return "1.19.1";
            },
            TimeSpan.FromMilliseconds(50));

        Assert.Null(await gate.FilterAsync("a1", TestContext.Current.CancellationToken));
        Assert.Equal("a1", await gate.FilterAsync("a1", TestContext.Current.CancellationToken));
        Assert.Equal(2, lookups);
    }

    [Fact]
    public async Task FilterAsync_ConcurrentCallers_ShareOneLookup()
    {
        var lookups = 0;
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new ActorActivationIdGate(_ =>
        {
            Interlocked.Increment(ref lookups);
            return release.Task;
        });

        var callers = Enumerable.Range(0, 16)
            .Select(i => Task.Run(() => gate.FilterAsync($"a{i}", TestContext.Current.CancellationToken)))
            .ToArray();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        release.SetResult("1.19.1");
        var results = await Task.WhenAll(callers);

        Assert.Equal(1, lookups);
        Assert.Equal(Enumerable.Range(0, 16).Select(i => $"a{i}"), results);
    }

    [Fact]
    public async Task FilterAsync_CallerCancelled_Throws()
    {
        var gate = new ActorActivationIdGate(_ => new TaskCompletionSource<string?>().Task);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.FilterAsync("a1", cts.Token));
    }
}
