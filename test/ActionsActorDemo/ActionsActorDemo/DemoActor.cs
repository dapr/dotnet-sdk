using Microsoft.Actions.Actors.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IDemoActorIneterface;
using Microsoft.Actions.Actors;
using Newtonsoft.Json.Linq;

namespace ActionsActorDemo
{
    public class DemoActor : Actor, IDemoActor, IRemindable
    {
        private IActorReminder reminder;

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            // Provides opportunity to perform some optional setup.
            Console.WriteLine($"Activating actor id: {this.Id}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called whenever an actor is deactivated after a period of inactivity.
        /// </summary>
        protected override Task OnDeactivateAsync()
        {
            // Provides Opporunity to perform optional cleanup.
            Console.WriteLine($"Dectivating actor id: {this.Id}");
            return Task.CompletedTask;
        }

        public DemoActor(ActorService service, ActorId actorId) : base(service, actorId)
        {   
        }

        public async Task<string> ProcessData(MyData data)
        {
            // this.StateManager.AddOrUpdateState<T>();
            // this.StateManager.SaveStateAsync();
            Console.WriteLine($"This is Actor id {this.Id}  with data {data.ToString()}");
            await this.StateManager.SetStateAsync<MyData>("my_data", data);
            return "Success";
        }

        public Task<MyData> Echo(MyData data)
        {
            return Task.FromResult(data);
        }

        public Task<MyData> GetData()
        {
            return this.StateManager.GetStateAsync<MyData>("my_data");
        }

        public Task<string> ProcessStringData(string data)
        {
            Console.WriteLine($"This is Actor id {this.Id}  with data {data}");
            return Task.FromResult("Success");
        }

        public Task NoReturnTypeNoArg()
        {
            return Task.CompletedTask;
        }

        public Task ThrowException()
        {
            throw new NotImplementedException();
        }

        public async Task RegisterReminder()
        {
            this.reminder =  await this.RegisterReminderAsync("Test", null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));            
        }

        public Task UnregisterReminder()
        {
            return this.UnregisterReminderAsync("Test");
        }

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            return Task.CompletedTask;
        }

        public Task RegisterTimer()
        {
            return this.RegisterTimerAsync("Test", this.TimerCallBack, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public Task UnregisterTimer()
        {
            return this.UnregisterTimerAsync("Test");
        }

        private Task TimerCallBack(object data)
        {
            return Task.CompletedTask;
        }
    }
}
