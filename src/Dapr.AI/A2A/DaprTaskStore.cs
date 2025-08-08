using A2A;
using Dapr.Client;

/// <summary>
/// Represents a Dapr-backed state store implementation for the A2A Dotnet ITaskStore interface, allowing agents to keep persistent state.
/// </summary>
public class DaprTaskStore : ITaskStore
{
    private readonly DaprClient _daprClient;
    private readonly string _stateStoreName;

    // Dapr state operation settings: strong consistency, with default concurrency (override per operation as needed)
    private static readonly StateOptions StrongConsistency = new StateOptions
    {
        Consistency = ConsistencyMode.Strong  // Ensure reads/writes use strong consistency
    };

    /// <summary>
    /// Constructor for the Task Store.
    /// </summary>
    /// <param name="daprClient">A Dapr Client insance</param>
    /// <param name="stateStoreName">The name of the state store component to use</param>
    public DaprTaskStore(DaprClient daprClient, string stateStoreName = "statestore")
    {
        _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
        _stateStoreName = stateStoreName;
    }

    /// <summary>
    /// Retrieves a task by its ID.
    /// </summary>
    /// <param name="taskId">The ID of the task to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The task if found, null otherwise.</returns>
    public async Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (taskId == null) throw new ArgumentNullException(nameof(taskId));

        // Retrieve the AgentTask from Dapr state store with strong consistency to get the latest data
        AgentTask? task = await _daprClient.GetStateAsync<AgentTask>(
                              _stateStoreName,
                              key: taskId,
                              consistencyMode: ConsistencyMode.Strong,
                              metadata: null,
                              cancellationToken: cancellationToken);
        return task;
    }

    /// <summary>
    /// Stores or updates a task.
    /// </summary>
    /// <param name="task">The task to store.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task SetTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));
        // The task.Id will be used as the key. We save the entire AgentTask object.
        // Use strong consistency on write; concurrency defaults to last-write-wins for new entries.
        await _daprClient.SaveStateAsync(
            _stateStoreName,
            key: task.Id,
            value: task,
            stateOptions: StrongConsistency,   // strong consistency ensures durability before ack
            metadata: null,
            cancellationToken: cancellationToken
        );
        // Note: If the task already existed, this will overwrite it (last-write-wins behavior since no ETag used).
    }

    /// <summary>
    /// Updates the status of a task.
    /// </summary>
    /// <param name="taskId">The ID of the task.</param>
    /// <param name="status">The new status.</param>
    /// <param name="message">Optional message associated with the status.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The updated task status.</returns>
    public async Task<AgentTaskStatus> UpdateStatusAsync(string taskId, TaskState status, Message? message = null, CancellationToken cancellationToken = default)
    {
        if (taskId == null) throw new ArgumentNullException(nameof(taskId));
        // Fetch state with its ETag for concurrency control.
        // We use strong consistency to get the latest state and ETag.
        var (existingTask, etag) = await _daprClient.GetStateAndETagAsync<AgentTask>(
                                        _stateStoreName,
                                        key: taskId,
                                        consistencyMode: ConsistencyMode.Strong,
                                        metadata: null,
                                        cancellationToken: cancellationToken);
        if (existingTask == null)
        {
            throw new KeyNotFoundException($"Task with ID '{taskId}' not found.");
        }

        // Update the status field of the retrieved task object.
        var st = existingTask.Status;
        st.State = status;
        if (message != null)
        {
            st.Message = message;
        }

        existingTask.Status = st;

        // Attempt to save the updated task back with the ETag for optimistic concurrency.
        var stateOptions = new StateOptions
        {
            Consistency = ConsistencyMode.Strong,
            Concurrency = ConcurrencyMode.FirstWrite  // enable first-write-wins
        };
        bool saved = await _daprClient.TrySaveStateAsync(
                         _stateStoreName,
                         key: taskId,
                         value: existingTask,
                         etag: etag,                // use ETag to ensure no concurrent modification
                         stateOptions: stateOptions,
                         metadata: null,
                         cancellationToken: cancellationToken
                     );
        if (!saved)
        {
            // The save failed due to an ETag mismatch (concurrent update happened).
            throw new InvalidOperationException($"Concurrent update detected for task '{taskId}'. Update was not saved.");
        }

        return existingTask.Status;
    }

    /// <summary>
    /// Retrieves push notification configuration for a task.
    /// </summary>
    /// <param name="taskId">The ID of the task.</param>
    /// <param name="notificationConfigId">The ID of the push notification configuration.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The push notification configuration if found, null otherwise.</returns>
    public Task<TaskPushNotificationConfig?> GetPushNotificationAsync(string taskId, string notificationConfigId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores push notification configuration for a task.
    /// </summary>
    /// <param name="pushNotificationConfig">The push notification configuration.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public Task SetPushNotificationConfigAsync(TaskPushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieves push notification configuration for a task.
    /// </summary>
    /// <param name="taskId">The ID of the task.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The push notification configuration if found, null otherwise.</returns>
    public Task<IEnumerable<TaskPushNotificationConfig>> GetPushNotificationsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
