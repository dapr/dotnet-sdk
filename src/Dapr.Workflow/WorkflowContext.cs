using System.Text.Json;
using Microsoft.DurableTask;

namespace Dapr.Workflow;

public class WorkflowsContext
{
    readonly TaskOrchestrationContext innerContext;

    internal WorkflowsContext(TaskOrchestrationContext innerContext)
    {
        this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
    }

    public TaskName Name => this.innerContext.Name;
    public string InstanceId => this.innerContext.InstanceId;

    public DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

    public void SetCustomStatus(object? customStatus) => this.innerContext.SetCustomStatus(customStatus);

    public Task CreateTimer(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return this.innerContext.CreateTimer(delay, cancellationToken);
    }

    public Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout)
    {
        return this.innerContext.WaitForExternalEvent<T>(eventName, timeout);
    }

    public Task CallActivityAsync(TaskName name, object? input = null, TaskOptions? options = null)
    {
        return this.innerContext.CallActivityAsync<object>(name, input, options);
    }
}