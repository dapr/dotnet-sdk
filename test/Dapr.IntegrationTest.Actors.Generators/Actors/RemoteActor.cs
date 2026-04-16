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
internal sealed class RemoteActor : Actor, IRemoteActor
{
    private readonly ILogger<RemoteActor> logger;

    private RemoteState currentState = new("default");

    private int callCount;

    public RemoteActor(ActorHost host, ILogger<RemoteActor> logger)
        : base(host)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task<RemoteState> GetState()
    {
        this.logger.LogInformation("GetState called.");
        return Task.FromResult(this.currentState);
    }

    /// <inheritdoc />
    public Task SetState(RemoteState state)
    {
        this.logger.LogInformation("SetState called.");
        this.currentState = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> SayHello(string name)
    {
        this.logger.LogInformation("SayHello called with name: {Name}", name);
        return Task.FromResult($"Hello, {name}!");
    }

    /// <inheritdoc />
    public Task IncrementCallCount()
    {
        this.logger.LogInformation("IncrementCallCount called.");
        this.callCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetCallCount()
    {
        this.logger.LogInformation("GetCallCount called.");
        return Task.FromResult(this.callCount);
    }

    /// <inheritdoc />
    public Task Ping(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
