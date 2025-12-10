using System;

namespace Dapr.Workflow.Client;

/// <summary>
/// Fluent builder for <see cref="StartWorkflowOptions"/>.
/// </summary>
public sealed class StartWorkflowOptionsBuilder
{
    private string? _instanceId;
    private DateTimeOffset? _startAt;

    /// <summary>
    /// Sets the instance ID for the workflow.
    /// </summary>
    public StartWorkflowOptionsBuilder WithInstanceId(string instanceId)
    {
        _instanceId = instanceId;
        return this;
    }

    /// <summary>
    /// Schedules the workflow to start at a specific date and time.
    /// </summary>
    public StartWorkflowOptionsBuilder StartAt(DateTimeOffset startAt)
    {
        _startAt = startAt;
        return this;
    }

    /// <summary>
    /// Schedules the workflow to start after a delay.
    /// </summary>
    public StartWorkflowOptionsBuilder StartAfter(TimeSpan delay)
    {
        _startAt = DateTimeOffset.UtcNow.Add(delay);
        return this;
    }

    /// <summary>
    /// Builds the <see cref="StartWorkflowOptions"/>.
    /// </summary>
    public StartWorkflowOptions Build() => new StartWorkflowOptions() { InstanceId = _instanceId, StartAt = _startAt };

    /// <summary>
    /// Implicit conversion to <see cref="StartWorkflowOptions"/>.
    /// </summary>
    public static implicit operator StartWorkflowOptions(StartWorkflowOptionsBuilder builder) => builder.Build();
}
