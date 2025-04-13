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

using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using Grpc.Core;

namespace Dapr.Actors.Runtime;

internal class DefaultActorTimerManager : ActorTimerManager
{
    private readonly IDaprInteractor interactor;

    public DefaultActorTimerManager(IDaprInteractor interactor)
    {
        this.interactor = interactor;
    }

    public override async Task RegisterReminderAsync(ActorReminder reminder)
    {
        if (reminder == null)
        {
            throw new ArgumentNullException(nameof(reminder));
        }

        var serialized = await SerializeReminderAsync(reminder);
        await this.interactor.RegisterReminderAsync(reminder.ActorType, reminder.ActorId.ToString(), reminder.Name, serialized);
    }

    public override async Task<IActorReminder> GetReminderAsync(ActorReminderToken token)
    {
        if (token == null)
        {
            throw new ArgumentNullException(nameof(token));
        }
            
        var response = await this.interactor.GetReminderAsync(token.ActorType, token.ActorId.ToString(), token.Name);
        if ((int)response.StatusCode == 500)
        {
            return null;
        }

        var responseStream = await response.Content.ReadAsStreamAsync();
        return await DeserializeReminderAsync(responseStream, token);
    }

    public override async Task UnregisterReminderAsync(ActorReminderToken reminder)
    {
        if (reminder == null)
        {
            throw new ArgumentNullException(nameof(reminder));
        }
            
        await this.interactor.UnregisterReminderAsync(reminder.ActorType, reminder.ActorId.ToString(), reminder.Name);
    }

    public override async Task RegisterTimerAsync(ActorTimer timer)
    {
        if (timer == null)
        {
            throw new ArgumentNullException(nameof(timer));
        }

#pragma warning disable 0618
        var timerInfo = new TimerInfo(timer.TimerCallback, timer.Data, timer.DueTime, timer.Period, timer.Ttl);
#pragma warning restore 0618
        var data = JsonSerializer.Serialize(timerInfo);
        await this.interactor.RegisterTimerAsync(timer.ActorType, timer.ActorId.ToString(), timer.Name, data);
    }

    public override async Task UnregisterTimerAsync(ActorTimerToken timer)
    {
        if (timer == null)
        {
            throw new ArgumentNullException(nameof(timer));
        }

        await this.interactor.UnregisterTimerAsync(timer.ActorType, timer.ActorId.ToString(), timer.Name);
    }

    private static async ValueTask<string> SerializeReminderAsync(ActorReminder reminder)
    {
        var info = new ReminderInfo(reminder.State, reminder.DueTime, reminder.Period, reminder.Repetitions, 
            reminder.Ttl);
        return await info.SerializeAsync();
    }

    private static async ValueTask<ActorReminder> DeserializeReminderAsync(Stream stream, ActorReminderToken token)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }
            
        var info = await ReminderInfo.DeserializeAsync(stream);
        if (info == null)
        {
            return null;
        }
            
        var reminder = new ActorReminder(token.ActorType, token.ActorId, token.Name, info.Data, info.DueTime, 
            info.Period);
        return reminder;
    }
}