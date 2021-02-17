// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace ActorClient
{
    using System;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.Actors.Client;
    using Dapr.Actors.Communication;
    using IDemoActorInterface;

    /// <summary>
    /// Actor Client class.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            var data = new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            };

            // Create an actor Id.
            var actorId = new ActorId("abc");

            // Make strongly typed Actor calls with Remoting.
            // DemoActor is the type registered with Dapr runtime in the service.
            var proxy = ActorProxy.Create<IDemoActor>(actorId, "DemoActor");

            Console.WriteLine("Making call using actor proxy to save data.");
            await proxy.SaveData(data);
            Console.WriteLine("Making call using actor proxy to get data.");
            var receivedData = await proxy.GetData();
            Console.WriteLine($"Received data is {receivedData}.");

            // Making some more calls to test methods.
            try
            {
                Console.WriteLine("Making calls to an actor method which has no argument and no return type.");
                await proxy.TestNoArgumentNoReturnType();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Got exception while making call to method with No Argument & No Return Type. Exception: {ex}");
            }

            try
            {
                await proxy.TestThrowException();
            }
            catch (ActorMethodInvocationException ex)
            {
                if (ex.InnerException is NotImplementedException)
                {
                    Console.WriteLine($"Got Correct Exception from actor method invocation.");
                }
                else
                {
                    Console.WriteLine($"Got Incorrect Exception from actor method invocation. Exception {ex.InnerException}");
                }
            }

            // Making calls without Remoting, this shows method invocation using InvokeMethodAsync methods, the method name and its payload is provided as arguments to InvokeMethodAsync methods.
            Console.WriteLine("Making calls without Remoting.");
            var nonRemotingProxy = ActorProxy.Create(actorId, "DemoActor");
            await nonRemotingProxy.InvokeMethodAsync("TestNoArgumentNoReturnType");
            await nonRemotingProxy.InvokeMethodAsync("SaveData", data);
            var res = await nonRemotingProxy.InvokeMethodAsync<MyData>("GetData");

            Console.WriteLine("Registering the timer and reminder");
            await proxy.RegisterTimer();
            await proxy.RegisterReminder();
            Console.WriteLine("Waiting so the timer and reminder can be triggered");
            await Task.Delay(6000);

            Console.WriteLine("Making call using actor proxy to get data after timer and reminder triggered");
            receivedData = await proxy.GetData();
            Console.WriteLine($"Received data is {receivedData}.");

            Console.WriteLine("Deregistering timer. Timers would any way stop if the actor is deactivated as part of Dapr garbage collection.");
            await proxy.UnregisterTimer();
            Console.WriteLine("Deregistering reminder. Reminders are durable and would not stop until an explicit deregistration or the actor is deleted.");
            await proxy.UnregisterReminder();


            Console.WriteLine("Creating a Bank Actor");
            var bank = ActorProxy.Create<IBankActor>(ActorId.CreateRandom(), "DemoActor");
            while (true)
            {
                var balance = await bank.GetAccountBalance();
                Console.WriteLine($"Balance for account '{balance.AccountId}' is '{balance.Balance:c}'.");

                Console.WriteLine($"Withdrawing '{10m:c}'...");
                try
                {
                    await bank.Withdraw(new WithdrawRequest() { Amount = 10m, });
                }
                catch (ActorMethodInvocationException ex)
                {
                    Console.WriteLine("Overdraft: " + ex.Message);
                    break;
                }
            }
        }
    }
}
