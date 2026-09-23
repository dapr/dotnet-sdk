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
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Reentrancy;

/// <summary>
/// Reproduces dapr/dapr#10532. Activation writes the shared key on the default
/// state tracker. An ordinary method call then writes the same key on its own
/// reentrancy-scoped tracker. A reminder callback, which also runs on the
/// default tracker, must observe that later write rather than the value cached
/// during activation.
/// </summary>
public class ReentrantStateActor : Actor, IReentrantStateActor, IRemindable
{
    private const string SharedKey = "reentrant-shared-value";
    private const string SeenKey = "reentrant-value-seen-by-reminder";
    private const string ReminderName = "reentrant-state-reminder";

    public ReentrantStateActor(ActorHost host)
        : base(host)
    {
    }

    public Task Ping()
    {
        return Task.CompletedTask;
    }

    // Caches the shared key on the default tracker, which is what later goes stale.
    protected override async Task OnActivateAsync()
    {
        await this.StateManager.SetStateAsync(SharedKey, "activation");
        await this.StateManager.SaveStateAsync();
    }

    public async Task SetValue(string value)
    {
        await this.StateManager.SetStateAsync(SharedKey, value);
    }

    public Task StartReminder()
    {
        return this.RegisterReminderAsync(ReminderName, Array.Empty<byte>(), dueTime: TimeSpan.Zero, period: TimeSpan.FromMilliseconds(500));
    }

    public async Task<string> GetValueSeenByReminder()
    {
        var seen = await this.StateManager.TryGetStateAsync<string>(SeenKey);
        return seen.HasValue ? seen.Value : string.Empty;
    }

    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        if (reminderName != ReminderName)
        {
            return;
        }

        var shared = await this.StateManager.GetStateAsync<string>(SharedKey);
        await this.StateManager.SetStateAsync(SeenKey, shared);
    }
}
