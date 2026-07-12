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

using Dapr.Actors.Next.Core.Runtime;

namespace Dapr.Actors.Next.Core.Scheduling;

internal sealed class ActorTurnWork
{
    private readonly TaskCompletionSource<byte[]?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration cancellationRegistration;

    public ActorTurnWork(ActorRuntimeRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        CancellationToken = cancellationToken;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(
                static state =>
                {
                    var work = (ActorTurnWork)state!;
                    work.completion.TrySetCanceled(work.CancellationToken);
                },
                this);
        }
    }

    public ActorRuntimeRequest Request { get; }

    public CancellationToken CancellationToken { get; }

    public Task<byte[]?> Task => completion.Task;

    public void Complete(byte[]? value)
    {
        cancellationRegistration.Dispose();
        completion.TrySetResult(value);
    }

    public void Fail(Exception exception)
    {
        cancellationRegistration.Dispose();
        completion.TrySetException(exception);
    }
}
