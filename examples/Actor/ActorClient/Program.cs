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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using IDemoActor;

var data = new MyData("ValueA", "ValueB");

// Create an actor Id.
var actorId = new ActorId("abc");

// Make strongly typed Actor calls with Remoting.
// DemoActor is the type registered with Dapr runtime in the service.
var proxy = ActorProxy.Create<IDemoActor.IDemoActor>(actorId, "DemoActor");

Console.WriteLine("Making call using actor proxy to save data.");
await proxy.SaveData(data, TimeSpan.FromMinutes(10));
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
    if (ex.InnerException is ActorInvokeException invokeEx && invokeEx.ActualExceptionType is "System.NotImplementedException")
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
await nonRemotingProxy.InvokeMethodAsync<MyData>("GetData");

Console.WriteLine("Registering the timer and reminder");
await proxy.RegisterTimer();
await proxy.RegisterReminder();
Console.WriteLine("Waiting so the timer and reminder can be triggered");
await Task.Delay(6000);

Console.WriteLine("Making call using actor proxy to get data after timer and reminder triggered");
receivedData = await proxy.GetData();
Console.WriteLine($"Received data is {receivedData}.");

Console.WriteLine("Getting details of the registered reminder");
var reminder = await proxy.GetReminder();
Console.WriteLine($"Received reminder is {reminder}.");

Console.WriteLine("Deregistering timer. Timers would any way stop if the actor is deactivated as part of Dapr garbage collection.");
await proxy.UnregisterTimer();
Console.WriteLine("Deregistering reminder. Reminders are durable and would not stop until an explicit deregistration or the actor is deleted.");
await proxy.UnregisterReminder();
            
Console.WriteLine("Registering reminder with repetitions - The reminder will repeat 3 times.");
await proxy.RegisterReminderWithRepetitions(3);
Console.WriteLine("Waiting so the reminder can be triggered");
await Task.Delay(5000);
Console.WriteLine("Getting details of the registered reminder");
reminder = await proxy.GetReminder();
Console.WriteLine($"Received reminder is {reminder?.ToString() ?? "None"} (expecting None).");
Console.WriteLine("Registering reminder with ttl and repetitions, i.e. reminder stops when either condition is met - The reminder will repeat 2 times.");
await proxy.RegisterReminderWithTtlAndRepetitions(TimeSpan.FromSeconds(5), 2);
Console.WriteLine("Getting details of the registered reminder");
reminder = await proxy.GetReminder();
Console.WriteLine($"Received reminder is {reminder}.");
Console.WriteLine("Deregistering reminder. Reminders are durable and would not stop until an explicit deregistration or the actor is deleted.");
await proxy.UnregisterReminder();

Console.WriteLine("Registering reminder and Timer with TTL - The reminder will self delete after 10 seconds.");
await proxy.RegisterReminderWithTtl(TimeSpan.FromSeconds(10));
await proxy.RegisterTimerWithTtl(TimeSpan.FromSeconds(10));
Console.WriteLine("Getting details of the registered reminder");
reminder = await proxy.GetReminder();
Console.WriteLine($"Received reminder is {reminder}.");

// Track the reminder.
var timer = new Timer(async state => Console.WriteLine($"Received data: {await proxy.GetData()}"), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
await Task.Delay(TimeSpan.FromSeconds(21));
await timer.DisposeAsync();

Console.WriteLine("Creating a Bank Actor");
var bank = ActorProxy.Create<IBankActor>(ActorId.CreateRandom(), "DemoActor");
while (true)
{
    var balance = await bank.GetAccountBalance();
    Console.WriteLine($"Balance for account '{balance.AccountId}' is '{balance.Balance:c}'.");

    Console.WriteLine($"Withdrawing '{10m:c}'...");
    try
    {
        await bank.Withdraw(new WithdrawRequest(10m));
    }
    catch (ActorMethodInvocationException ex)
    {
        Console.WriteLine($"Overdraft: {ex.Message}");
        break;
    }
}
