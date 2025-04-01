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
using Dapr.Actors.Runtime;
using IDemoActor;

namespace DemoActor;

// The following example showcases a few features of Actors
//
// Every actor should inherit from the Actor type, and must implement one or more actor interfaces.
// In this case the actor interfaces are DemoActor.Interfaces and IBankActor.
// 
// For Actors to use Reminders, it must derive from IRemindable.
// If you don't intend to use Reminder feature, you can skip implementing IRemindable and reminder 
// specific methods which are shown in the code below.
public class DemoActor(ActorHost host, BankService bank) : Actor(host), IDemoActor.IDemoActor, IBankActor, IRemindable
{
    private const string StateName = "my_data";

    public async Task SaveData(MyData data, TimeSpan ttl)
    {
        Console.WriteLine($"This is Actor id {this.Id} with data {data}.");

        // Set State using StateManager, state is saved after the method execution.
        await this.StateManager.SetStateAsync(StateName, data, ttl);
    }

    public Task<MyData> GetData()
    {
        // Get state using StateManager.
        return this.StateManager.GetStateAsync<MyData>(StateName);
    }

    public Task TestThrowException()
    {
        throw new NotImplementedException();
    }

    public Task TestNoArgumentNoReturnType()
    {
        return Task.CompletedTask;
    }

    public async Task RegisterReminder()
    {
        await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public async Task RegisterReminderWithTtl(TimeSpan ttl)
    {
        await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), ttl);
    }
        
    public async Task RegisterReminderWithRepetitions(int repetitions)
    {
        await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), repetitions);
    }
        
    public async Task RegisterReminderWithTtlAndRepetitions(TimeSpan ttl, int repetitions)
    {
        await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), repetitions, ttl);
    }

    public async Task<ActorReminderData?> GetReminder()
    {
        var reminder = await this.GetReminderAsync("TestReminder");

        return reminder is not null
            ? new ActorReminderData
            {
                Name = reminder.Name,
                Period = reminder.Period,
                DueTime = reminder.DueTime
            }
            : null;
    }
        
    public Task UnregisterReminder()
    {
        return this.UnregisterReminderAsync("TestReminder");
    }

    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        // This method is invoked when an actor reminder is fired.
        var actorState = await this.StateManager.GetStateAsync<MyData>(StateName);
        var updatedActorState = actorState with
        {
            PropertyB = $"Reminder triggered at '{DateTime.Now:yyyy-MM-ddTHH:mm:ss}'"
        };
        await this.StateManager.SetStateAsync<MyData>(StateName, updatedActorState, ttl: TimeSpan.FromMinutes(5));
    }

    class TimerParams
    {
        public int IntParam { get; set; }
        public string? StringParam { get; set; }
    }

    /// <inheritdoc/>
    public Task RegisterTimer()
    {
        var timerParams = new TimerParams
        {
            IntParam = 100,
            StringParam = "timer test",
        };

        var serializedTimerParams = JsonSerializer.SerializeToUtf8Bytes(timerParams);
        return this.RegisterTimerAsync("TestTimer", nameof(this.TimerCallback), serializedTimerParams, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    public Task RegisterTimerWithTtl(TimeSpan ttl)
    {
        var timerParams = new TimerParams
        {
            IntParam = 100,
            StringParam = "timer test",
        };

        var serializedTimerParams = JsonSerializer.SerializeToUtf8Bytes(timerParams);
        return this.RegisterTimerAsync("TestTimer", nameof(this.TimerCallback), serializedTimerParams, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), ttl);
    }

    public Task UnregisterTimer()
    {
        return this.UnregisterTimerAsync("TestTimer");
    }

    // This method is called whenever an actor is activated.
    // An actor is activated the first time any of its methods are invoked.
    protected override Task OnActivateAsync()
    {
        // Provides opportunity to perform some optional setup.
        return Task.CompletedTask;
    }

    // This method is called whenever an actor is deactivated after a period of inactivity.
    protected override Task OnDeactivateAsync()
    {
        // Provides Opportunity to perform optional cleanup.
        return Task.CompletedTask;
    }

    /// <summary>
    /// This method is called when the timer is triggered based on its registration.
    /// It updates the PropertyA value.
    /// </summary>
    /// <param name="data">Timer input data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task TimerCallback(byte[] data)
    {
        var state = await this.StateManager.GetStateAsync<MyData>(StateName);
        var updatedState = state with { PropertyA = $"Timer triggered at '{DateTime.Now:yyyyy-MM-ddTHH:mm:s}'" };
        await this.StateManager.SetStateAsync<MyData>(StateName, updatedState, ttl: TimeSpan.FromMinutes(5));
        var timerParams = JsonSerializer.Deserialize<TimerParams>(data);
        if (timerParams != null)
        {
            Console.WriteLine($"Timer parameter1: {timerParams.IntParam}");
            Console.WriteLine($"Timer parameter2: {timerParams.StringParam ?? ""}");
        }
    }

    public async Task<AccountBalance> GetAccountBalance()
    {
        var starting = new AccountBalance(this.Id.GetId(), 100m); // Start new accounts with 100 million; we're pretty generous

        var balance = await this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting);
        return balance;
    }

    public async Task Withdraw(WithdrawRequest withdraw)
    {
        var starting = new AccountBalance(this.Id.GetId(), 100m); // Start new accounts with 100 million; we're pretty generous.

        var balance = await this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting);

        // Throws Overdraft exception if the account doesn't have enough money.
        var updated = bank.Withdraw(balance.Balance, withdraw.Amount);

        balance = balance with { Balance = updated };
        await this.StateManager.SetStateAsync("balance", balance);
    }
}
