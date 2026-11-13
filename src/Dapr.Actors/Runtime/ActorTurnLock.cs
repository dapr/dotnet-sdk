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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Actors.Runtime;

// The sidecar can stop waiting before actor code finishes. Keep local ownership until
// every invocation in the turn has completed, not until its request is cancelled.
internal sealed class ActorTurnLock
{
    private readonly Dictionary<ActorId, Entry> entries = new Dictionary<ActorId, Entry>();
    private readonly object sync = new object();

    public async Task<Lease> AcquireAsync(ActorId actorId, string reentrancyId, CancellationToken cancellationToken)
    {
        Entry entry;
        lock (this.sync)
        {
            if (!this.entries.TryGetValue(actorId, out entry))
            {
                entry = new Entry();
                this.entries.Add(actorId, entry);
            }

            entry.References++;
        }

        try
        {
            while (true)
            {
                Task available;
                lock (this.sync)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.ActiveCalls == 0 ||
                        (entry.ReentrancyAllowed && reentrancyId != null && entry.ReentrancyId == reentrancyId))
                    {
                        var reentered = entry.ActiveCalls > 0;
                        if (!reentered)
                        {
                            entry.ReentrancyId = reentrancyId;
                            entry.ReentrancyAllowed = false;
                        }

                        entry.ActiveCalls++;
                        return new Lease(this, actorId, entry, reentered);
                    }

                    available = entry.Available.Task;
                }

                await available.WaitAsync(cancellationToken);
            }
        }
        catch
        {
            lock (this.sync)
            {
                this.ReleaseReference(actorId, entry);
            }

            throw;
        }
    }

    public bool IsInChain(ActorId actorId, string reentrancyId)
    {
        if (reentrancyId == null)
        {
            return false;
        }

        lock (this.sync)
        {
            return this.entries.TryGetValue(actorId, out var entry) && entry.ActiveCalls > 0 && entry.ReentrancyId == reentrancyId;
        }
    }

    private void Release(ActorId actorId, Entry entry)
    {
        lock (this.sync)
        {
            entry.ActiveCalls--;
            if (entry.ActiveCalls == 0)
            {
                entry.ReentrancyId = null;
                entry.ReentrancyAllowed = false;
                entry.WakeWaiters();
            }

            this.ReleaseReference(actorId, entry);
        }
    }

    private void ReleaseReference(ActorId actorId, Entry entry)
    {
        // Waiters also hold references, so removing an idle entry cannot create two locks for one ID.
        if (--entry.References == 0)
        {
            this.entries.Remove(actorId);
        }
    }

    internal sealed class Entry
    {
        public int References;
        public int ActiveCalls;
        public string ReentrancyId;
        public bool ReentrancyAllowed;

        public TaskCompletionSource<bool> Available =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public void WakeWaiters()
        {
            var available = this.Available;
            this.Available = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            available.SetResult(true);
        }
    }

    internal sealed class Lease : IDisposable
    {
        private ActorTurnLock owner;
        private readonly ActorId actorId;
        private readonly Entry entry;

        internal Lease(ActorTurnLock owner, ActorId actorId, Entry entry, bool reentered)
        {
            this.owner = owner;
            this.actorId = actorId;
            this.entry = entry;
            this.Reentered = reentered;
        }

        public bool Reentered { get; }

        public void AllowReentrancy()
        {
            lock (this.owner.sync)
            {
                // Activation must finish before even a matching call chain can enter.
                if (!this.entry.ReentrancyAllowed && this.entry.ReentrancyId != null)
                {
                    this.entry.ReentrancyAllowed = true;
                    this.entry.WakeWaiters();
                }
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref this.owner, null)?.Release(this.actorId, this.entry);
        }
    }
}
