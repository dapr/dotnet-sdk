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
using System.Threading.Tasks;
using Dapr.Actors;

namespace IDemoActor;

/// <summary>
/// Interface for Actor method.
/// </summary>
public interface IDemoActor : IActor
{
    /// <summary>
    /// Method to save data.
    /// </summary>
    /// <param name="data">DAta to save.</param>
    /// <param name="ttl">TTL of state key.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveData(MyData data, TimeSpan ttl);

    /// <summary>
    /// Method to get data.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task<MyData> GetData();

    /// <summary>
    /// A test method which throws exception.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task TestThrowException();

    /// <summary>
    /// A test method which validates calls for methods with no arguments and no return types.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task TestNoArgumentNoReturnType();

    /// <summary>
    /// Registers a reminder.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task RegisterReminder();

    /// <summary>
    /// Registers a reminder.
    /// </summary>
    /// <param name="ttl">TimeSpan that dictates when the reminder expires.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task RegisterReminderWithTtl(TimeSpan ttl);

    /// <summary>
    /// Unregisters the registered reminder.
    /// </summary>
    /// <returns>Task representing the operation.</returns>
    Task UnregisterReminder();

    /// <summary>
    /// Registers a timer.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task RegisterTimer();

    /// <summary>
    /// Registers a timer.
    /// </summary>
    /// <param name="ttl">Optional TimeSpan that dictates when the timer expires.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task RegisterTimerWithTtl(TimeSpan ttl);
        
    /// <summary>
    /// Registers a reminder with repetitions.
    /// </summary>
    /// <param name="repetitions">The number of repetitions for which the reminder should be invoked.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task RegisterReminderWithRepetitions(int repetitions);
        
    /// <summary>
    /// Registers a reminder with ttl and repetitions.
    /// </summary>
    /// <param name="ttl">TimeSpan that dictates when the timer expires.</param>
    /// <param name="repetitions">The number of repetitions for which the reminder should be invoked.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task RegisterReminderWithTtlAndRepetitions(TimeSpan ttl, int repetitions);

    /// <summary>
    /// Gets the registered reminder.
    /// </summary>
    /// <returns>A task that returns the reminder after completion.</returns>
    Task<ActorReminderData?> GetReminder();

    /// <summary>
    /// Unregisters the registered timer.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task UnregisterTimer();
}

/// <summary>
/// Data Used by the Sample Actor.
/// </summary>
public sealed record MyData(string? PropertyA, string? PropertyB)
{
    /// <inheritdoc/>
    public override string ToString() => $"PropertyA: {PropertyA ?? "null"}, PropertyB: {PropertyB ?? "null"}";
}

public class ActorReminderData
{
    public string? Name { get; set; }

    public TimeSpan DueTime { get; set; }

    public TimeSpan Period { get; set; }

    public override string ToString() => $"Name: {this.Name}, DueTime: {this.DueTime}, Period: {this.Period}";
}
