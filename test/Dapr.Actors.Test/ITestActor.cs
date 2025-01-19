// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors.Test;

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

/// <summary>
/// Interface for test actor.
/// </summary>
public interface ITestActor : IActor
{
    /// <summary>
    /// GetCount method for TestActor.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The current count as stored in actor.</returns>
    Task<int> GetCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// SetCount method for test actor.
    /// </summary>
    /// <param name="count">Count to set for the actor.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Task.</returns>
    Task SetCountAsync(int count, CancellationToken cancellationToken);
}

/// <summary>
/// Test Actor Class.
/// </summary>
public class TestActor : Actor,  ITestActor
{
    public TestActor(ActorHost host, IActorStateManager stateManager = null)
        : base(host)
    {
        if (stateManager != null)
        {
            this.StateManager = stateManager;
        }
    }

    /// <inheritdoc/>
    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(5);
    }

    /// <inheritdoc/>
    public Task SetCountAsync(int count, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task SaveTestState()
    {
        return this.SaveStateAsync();
    }

    public Task ResetTestStateAsync()
    {
        return this.ResetStateAsync();
    }

    public void TimerCallbackNonTaskReturnType()
    {
    }

    public Task TimerCallbackTwoArguments(int i, int j)
    {
        Console.WriteLine(i + j);
        return default;
    }

    private Task TimerCallbackPrivate()
    {
        return default;
    }

    protected Task TimerCallbackProtected()
    {
        return default;
    }

    internal Task TimerCallbackInternal()
    {
        return default;
    }

    public Task TimerCallbackPublicWithNoArguments()
    {
        return default;
    }

    public Task TimerCallbackPublicWithOneArgument(int i)
    {
        return default;
    }

    public Task TimerCallbackOverloaded()
    {
        return default;
    }

    public Task TimerCallbackOverloaded(int i)
    {
        return default;
    }

    public static Task TimerCallbackStatic()
    {
        return default;
    }
}
