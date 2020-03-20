﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace DaprDemoActor
{
    using System;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.Actors.Runtime;
    using IDemoActorInterface;

    /// <summary>
    /// Actor Implementation.
    /// Following example shows how to use Actor Reminders as well.
    /// For Actors to use Reminders, it must derive from IRemindable.
    /// If you don't intend to use Reminder feature, you can skip implementing IRemindable and reminder specific methods which are shown in the code below.
    /// </summary>
    public class DemoActor : Actor, IDemoActor, IRemindable
    {
        private const string StateName = "my_data";

        /// <summary>
        /// Initializes a new instance of the <see cref="DemoActor"/> class.
        /// </summary>
        /// <param name="service">Actor Service hosting the actor.</param>
        /// <param name="actorId">Actor Id.</param>
        public DemoActor(ActorService service, ActorId actorId)
            : base(service, actorId)
        {
        }

        /// <inheritdoc/>
        public async Task SaveData(MyData data)
        {
            Console.WriteLine($"This is Actor id {this.Id} with data {data}.");

            // Set State using StateManager, state is saved after the method execution.
            await this.StateManager.SetStateAsync<MyData>(StateName, data);
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
        public Task UnregisterReminder()
        {
            return this.UnregisterReminderAsync("TestReminder");
        }

        /// <inheritdoc/>
        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            // This method is invoked when an actor reminder is fired.
            var actorState = this.StateManager.GetStateAsync<MyData>(StateName).GetAwaiter().GetResult();
            actorState.PropertyB = $"Reminder triggered at '{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")}'";
            this.StateManager.SetStateAsync<MyData>(StateName, actorState);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task RegisterTimer()
        {
            return this.RegisterTimerAsync("TestTimer", this.TimerCallBack, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }

        /// <inheritdoc/>
        public Task UnregisterTimer()
        {
            return this.UnregisterTimerAsync("TestTimer");
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected override Task OnActivateAsync()
        {
            // Provides opportunity to perform some optional setup.
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called whenever an actor is deactivated after a period of inactivity.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
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
        private Task TimerCallBack(object data)
        {
            var state = this.StateManager.GetStateAsync<MyData>(StateName).GetAwaiter().GetResult();
            state.PropertyA = $"Timer triggered at '{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")}'";
            this.StateManager.SetStateAsync<MyData>(StateName, state);
            return Task.CompletedTask;
        }
    }
}
