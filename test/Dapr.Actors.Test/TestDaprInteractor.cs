using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Communication;

namespace Dapr.Actors;

/// <summary>
/// A Wrapper class for IDaprInteractor which is mainly created for testing.
/// </summary>
public class TestDaprInteractor : IDaprInteractor
{
    private TestDaprInteractor _testDaprInteractor;
        
    /// <summary>
    /// The TestDaprInteractor constructor.
    /// </summary>
    /// <param name="testDaprInteractor"></param>
    public TestDaprInteractor(TestDaprInteractor testDaprInteractor) 
    {
        _testDaprInteractor = testDaprInteractor;
    }
        
    /// <summary>
    /// The TestDaprInteractor constructor.
    /// </summary>
    public TestDaprInteractor() 
    {
            
    }

    /// <summary>
    /// Register a reminder.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="reminderName">Name of reminder to register.</param>
    /// <param name="data">JSON reminder data as per the Dapr spec.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public virtual async Task RegisterReminderAsync(string actorType, string actorId, string reminderName, string data,
        CancellationToken cancellationToken = default) 
    {
        await _testDaprInteractor.RegisterReminderAsync(actorType, actorId, reminderName, data);
    }

    /// <summary>
    /// Invokes an Actor method on Dapr without remoting.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="methodName">Method name to invoke.</param>
    /// <param name="jsonPayload">Serialized body.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<Stream> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, 
        string jsonPayload, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Saves state batch to Dapr.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="data">JSON data with state changes as per the Dapr spec for transaction state update.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task SaveStateTransactionallyAsync(string actorType, string actorId, string data,
        CancellationToken cancellationToken = default)
    {
        await _testDaprInteractor.SaveStateTransactionallyAsync(actorType, actorId, data);
    }

    /// <summary>
    /// Saves a state to Dapr.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="keyName">Name of key to get value for.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<ActorStateResponse<string>> GetStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default)
    {
        return await _testDaprInteractor.GetStateAsync(actorType, actorId, keyName);
    }

    /// <summary>
    /// Invokes Actor method.
    /// </summary>
    /// <param name="serializersManager">Serializers manager for remoting calls.</param>
    /// <param name="remotingRequestRequestMessage">Actor Request Message.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    Task<IActorResponseMessage> IDaprInteractor.InvokeActorMethodWithRemotingAsync(ActorMessageSerializersManager serializersManager,
        IActorRequestMessage remotingRequestRequestMessage, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Gets a reminder.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="reminderName">Name of reminder to unregister.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public Task<Stream> GetReminderAsync(string actorType, string actorId, string reminderName,
        CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Unregisters a reminder.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="reminderName">Name of reminder to unregister.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public Task UnregisterReminderAsync(string actorType, string actorId, string reminderName,
        CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Registers a timer.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="timerName">Name of timer to register.</param>
    /// <param name="data">JSON reminder data as per the Dapr spec.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public Task RegisterTimerAsync(string actorType, string actorId, string timerName, string data,
        CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Unregisters a timer.
    /// </summary>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="actorId">ActorId.</param>
    /// <param name="timerName">Name of timer to register.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public Task UnregisterTimerAsync(string actorType, string actorId, string timerName,
        CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }
}
