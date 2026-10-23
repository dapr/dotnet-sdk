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

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// A deterministic, cancellation-aware pause at an injected fault boundary.
/// </summary>
public sealed class ActorFaultPause : IDisposable
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal ActorFaultPause()
    {
    }

    /// <summary>
    /// Completes when the operation reaches the selected boundary.
    /// </summary>
    public Task Reached => reached.Task;

    /// <summary>
    /// Allows the paused operation to continue. Calling this before the boundary is reached is also safe.
    /// </summary>
    public void Release() => released.TrySetResult();

    /// <inheritdoc />
    public void Dispose() => Release();

    internal Task WaitAsync(CancellationToken cancellationToken)
    {
        reached.TrySetResult();
        return released.Task.WaitAsync(cancellationToken);
    }
}
