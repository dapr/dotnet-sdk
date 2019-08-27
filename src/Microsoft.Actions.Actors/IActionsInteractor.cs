// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;
    using Microsoft.Actions.Actors.Runtime;

    /// <summary>
    /// Interface for interacting with Actions runtime.
    /// </summary>
    internal interface IActionsInteractor
    {
        /// <summary>
        /// Invokes an Actor method on Actions without remoting.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="methodName">Method name to invoke.</param>
        /// <param name="jsonPayload">Serialized body.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<Stream> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves Actor state. This is temporary until the Actions runtime implements the Batch state update.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="keyName">state name.</param>
        /// <param name="data">State to be saved.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveStateAsync(string actorType, string actorId, string keyName, string data, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Removes Actor state. This is temporary until the Actions runtime implements the Batch state update.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="keyName">state name.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task RemoveStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves state batch to Actions.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="data">Json data with state changes as per the actions spec for transaction state update.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveStateTransationallyAsync(string actorType, string actorId, string data, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves a state to Actions.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="keyName">Name of key to get value for.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<byte[]> GetStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Invokes Actor method.
        /// </summary>
        /// <param name="serializersManager">Serializers manager for remoting calls.</param>
        /// <param name="remotingRequestRequestMessage">Actor Request Message.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task<IActorResponseMessage> InvokeActorMethodWithRemotingAsync(ActorMessageSerializersManager serializersManager, IActorRequestMessage remotingRequestRequestMessage, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Register a reminder.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="reminderName">Name of reminder to register.</param>
        /// <param name="data">Json reminder data as per the actions spec.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task RegisterReminderAsync(string actorType, string actorId, string reminderName, string data, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Unregisters a reminder.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="reminderName">Name of reminder to unregister.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task UnregisterReminderAsync(string actorType, string actorId, string reminderName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Registers a timer.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="timerName">Name of timer to register.</param>
        /// <param name="data">Json reminder data as per the actions spec.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task RegisterTimerAsync(string actorType, string actorId, string timerName, string data, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Unegisters a timer.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="timerName">Name of timer to register.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task UnregisterTimerAsync(string actorType, string actorId, string timerName, CancellationToken cancellationToken = default(CancellationToken));
    }
}
