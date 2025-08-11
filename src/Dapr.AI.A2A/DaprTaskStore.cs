using A2A;
using Dapr.Client;
using System.Collections.Concurrent;

namespace Dapr.AI.A2A;

/// <summary>
/// Represents a Dapr-backed state store implementation for the A2A Dotnet ITaskStore interface, allowing agents to keep persistent state.
/// </summary>
public class DaprTaskStore : ITaskStore
{
    private readonly DaprClient _daprClient;
    private readonly string _stateStoreName;

    private static string PushCfgKey(string taskId, string configId) => $"a2a:task:{taskId}:pushcfg:{configId}";
    private static string PushCfgIndexKey(string taskId) => $"a2a:task:{taskId}:pushcfg:index";

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
    public async Task<TaskPushNotificationConfig?> GetPushNotificationAsync(string taskId, string notificationConfigId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentNullException(nameof(taskId));
        if (string.IsNullOrWhiteSpace(notificationConfigId)) throw new ArgumentNullException(nameof(notificationConfigId));

        return await _daprClient.GetStateAsync<TaskPushNotificationConfig>(
            _stateStoreName,
            key: PushCfgKey(taskId, notificationConfigId),
            consistencyMode: ConsistencyMode.Strong,
            metadata: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stores push notification configuration for a task.
    /// </summary>
    /// <param name="pushNotificationConfig">The push notification configuration.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task SetPushNotificationConfigAsync(TaskPushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default)
    {
        if (pushNotificationConfig is null) throw new ArgumentNullException(nameof(pushNotificationConfig));

        // Adjust these property names if your model differs:
        var taskId = pushNotificationConfig.TaskId ?? throw new ArgumentException("Config.TaskId is required.");
        var configId = pushNotificationConfig.PushNotificationConfig.Id ?? throw new ArgumentException("Config.Id is required.");

        //Save/Upsert the config itself
        await _daprClient.SaveStateAsync(
            _stateStoreName,
            key: PushCfgKey(taskId, configId),
            value: pushNotificationConfig,
            stateOptions: StrongConsistency,
            metadata: null,
            cancellationToken: cancellationToken);

        // Add the configId to the per-task index with ETag (avoid races)
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var (index, etag) = await _daprClient.GetStateAndETagAsync<string[]>(
                _stateStoreName,
                key: PushCfgIndexKey(taskId),
                consistencyMode: ConsistencyMode.Strong,
                metadata: null,
                cancellationToken: cancellationToken);

            var list = (index ?? Array.Empty<string>()).ToList();
            if (!list.Contains(configId, StringComparer.Ordinal))
                list.Add(configId);

            var ok = await _daprClient.TrySaveStateAsync(
                _stateStoreName,
                key: PushCfgIndexKey(taskId),
                value: list.ToArray(),
                etag: etag,
                stateOptions: new StateOptions { Consistency = ConsistencyMode.Strong, Concurrency = ConcurrencyMode.FirstWrite },
                metadata: null,
                cancellationToken: cancellationToken);

            if (ok) break;

            // small backoff before retry
            await Task.Delay(50 * (attempt + 1), cancellationToken);
        }
    }

    /// <summary>
    /// Retrieves push notification configuration for a task.
    /// </summary>
    /// <param name="taskId">The ID of the task.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The push notification configuration if found, null otherwise.</returns>
    public async Task<IEnumerable<TaskPushNotificationConfig>> GetPushNotificationsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentNullException(nameof(taskId));

        var ids = await _daprClient.GetStateAsync<string[]>(
            _stateStoreName,
            key: PushCfgIndexKey(taskId),
            consistencyMode: ConsistencyMode.Strong,
            metadata: null,
            cancellationToken: cancellationToken) ?? Array.Empty<string>();

        if (ids.Length == 0) return Array.Empty<TaskPushNotificationConfig>();

        const int maxParallel = 8;
        using var gate = new SemaphoreSlim(maxParallel);
        var bag = new ConcurrentBag<TaskPushNotificationConfig>();

        await Task.WhenAll(ids.Select(async id =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var cfg = await _daprClient.GetStateAsync<TaskPushNotificationConfig>(
                    _stateStoreName,
                    key: PushCfgKey(taskId, id),
                    consistencyMode: ConsistencyMode.Strong,
                    metadata: null,
                    cancellationToken: cancellationToken);

                if (cfg is not null) bag.Add(cfg);
            }
            finally
            {
                gate.Release();
            }
        }));

        return bag.ToArray();
    }
}