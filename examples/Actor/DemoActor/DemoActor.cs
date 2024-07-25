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

namespace DaprDemoActor
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Actors.Runtime;
    using IDemoActorInterface;

    // The following example showcases a few features of Actors
    //
    // Every actor should inherit from the Actor type, and must implement one or more actor interfaces.
    // In this case the actor interfaces are IDemoActor and IBankActor.
    // 
    // For Actors to use Reminders, it must derive from IRemindable.
    // If you don't intend to use Reminder feature, you can skip implementing IRemindable and reminder 
    // specific methods which are shown in the code below.
    public class DemoActor : Actor, IDemoActor, IBankActor, IRemindable
    {
        private const string StateName = "my_data";

        private readonly BankService bank;

        /// <summary>
        /// Initializes a new instance of <see cref="DemoActor"/>.
        /// </summary>
        /// <param name="host">ActorHost.</param>
        /// <param name="bank">BankService.</param>
        /// <param name="actorStateManager">ActorStateManager used in UnitTests.</param>
        public DemoActor(
            ActorHost host,
            BankService bank,
            IActorStateManager actorStateManager = null)
            : base(host)
        {
            // BankService is provided by dependency injection.
            // See Program.cs
            this.bank = bank;

            // Assign ActorStateManager when passed as parameter.
            // This is used in UnitTests.
            if (actorStateManager != null)
            {
                this.StateManager = actorStateManager;
            }
        }

        /// <inheritdoc/>
        public async Task SaveData(MyDataWithTTL data)
        {
            Console.WriteLine($"This is Actor id {this.Id} with data {data}.");

            // Set State using StateManager, state is saved after the method execution.
            await this.StateManager.SetStateAsync<MyData>(StateName, data.MyData, data.TTL);
        }

        /// <inheritdoc/>
        public Task<MyData> GetData()
        {
            // Get state using StateManager.
            return this.StateManager.GetStateAsync<MyData>(StateName);
        }

        /// <inheritdoc/>
        public Task TestThrowException()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task TestNoArgumentNoReturnType()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task RegisterReminder()
        {
            await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        /// <inheritdoc/>
        public async Task RegisterReminderWithTtl(TimeSpan ttl)
        {
            await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), ttl);
        }

        /// <inheritdoc/>
        public async Task RegisterReminderWithRepetitions(int repetitions)
        {
            await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), repetitions);
        }

        /// <inheritdoc/>
        public async Task RegisterReminderWithTtlAndRepetitions(TimeSpan ttl, int repetitions)
        {
            await this.RegisterReminderAsync("TestReminder", null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), repetitions, ttl);
        }

        /// <inheritdoc/>
        public async Task<ActorReminderData> GetReminder()
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

        /// <inheritdoc/>
        public Task UnregisterReminder()
        {
            return this.UnregisterReminderAsync("TestReminder");
        }

        /// <inheritdoc/>
        public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            // This method is invoked when an actor reminder is fired.
            var actorState = await this.StateManager.GetStateAsync<MyData>(StateName);
            actorState.PropertyB = $"Reminder triggered at '{DateTime.Now:yyyy-MM-ddTHH:mm:ss}'";
            await this.StateManager.SetStateAsync<MyData>(StateName, actorState, ttl: TimeSpan.FromMinutes(5));
        }

        class TimerParams
        {
            public int IntParam { get; set; }
            public string StringParam { get; set; }
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
            state.PropertyA = $"Timer triggered at '{DateTime.Now:yyyyy-MM-ddTHH:mm:s}'";
            await this.StateManager.SetStateAsync<MyData>(StateName, state, ttl: TimeSpan.FromMinutes(5));
            var timerParams = JsonSerializer.Deserialize<TimerParams>(data);
            Console.WriteLine("Timer parameter1: " + timerParams.IntParam);
            Console.WriteLine("Timer parameter2: " + timerParams.StringParam);
        }

        /// <inheritdoc/>
        public async Task<AccountBalance> GetAccountBalance()
        {
            var starting = new AccountBalance()
            {
                AccountId = this.Id.GetId(),
                Balance = 100m, // Start new accounts with 100, we're pretty generous.
            };

            var balance = await this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting);
            return balance;
        }

        /// <inheritdoc/>
        public async Task Withdraw(WithdrawRequest withdraw)
        {
            var starting = new AccountBalance()
            {
                AccountId = this.Id.GetId(),
                Balance = 100m, // Start new accounts with 100, we're pretty generous.
            };

            var balance = await this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting);

            // Throws Overdraft exception if the account doesn't have enough money.
            var updated = this.bank.Withdraw(balance.Balance, withdraw.Amount);

            balance.Balance = updated;
            await this.StateManager.SetStateAsync("balance", balance);
        }
    }
}
