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

using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.Logging;

namespace Dapr.IntegrationTest.Actors.Generators.Actors;

/// <summary>
/// Implementation of <see cref="IRemoteActor"/> that manages state in memory.
/// </summary>
internal sealed partial class RemoteActor(ActorHost host, ILogger<RemoteActor> logger) : Actor(host), IRemoteActor
{
    private RemoteState currentState = new("default");

    private int callCount;

    /// <inheritdoc />
    public Task<RemoteState> GetState()
    {
        logger.LogGetState();
        return Task.FromResult(this.currentState);
    }

    /// <inheritdoc />
    public Task SetState(RemoteState state)
    {
        logger.LogSetState();
        this.currentState = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> SayHello(string name)
    {
        logger.LogSayHello(name);
        return Task.FromResult($"Hello, {name}!");
    }

    /// <inheritdoc />
    public Task IncrementCallCount()
    {
        logger.LogIncrementCallCount();
        this.callCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetCallCount()
    {
        logger.LogGetCallCount();
        return Task.FromResult(this.callCount);
    }

    /// <inheritdoc />
    public Task Ping(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal static partial class RemoteActorLogMessages
{
    [LoggerMessage(LogLevel.Information, "GetState called.")]
    public static partial void LogGetState(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "SetState called.")]
    public static partial void LogSetState(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "SayHello called with name: {Name}")]
    public static partial void LogSayHello(this ILogger logger, string name);

    [LoggerMessage(LogLevel.Information, "IncrementCallCount called.")]
    public static partial void LogIncrementCallCount(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "GetCallCount called.")]
    public static partial void LogGetCallCount(this ILogger logger);
}
