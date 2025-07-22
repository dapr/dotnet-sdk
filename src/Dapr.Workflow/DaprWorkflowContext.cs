// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

using System;
using Microsoft.DurableTask;
using System.Threading.Tasks;
using System.Threading;

class DaprWorkflowContext : WorkflowContext
{
    readonly TaskOrchestrationContext innerContext;

    internal DaprWorkflowContext(TaskOrchestrationContext innerContext)
    {
        this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
    }

    public override string Name => this.innerContext.Name;

    public override string InstanceId => this.innerContext.InstanceId;

    public override DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

    public override bool IsReplaying => this.innerContext.IsReplaying;
        
    public override Task CallActivityAsync(string name, object? input = null, WorkflowTaskOptions? options = null)
    {
        return WrapExceptions(this.innerContext.CallActivityAsync(name, input, options?.ToDurableTaskOptions()));
    }

    public override Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null)
    {
        return WrapExceptions(this.innerContext.CallActivityAsync<T>(name, input, options?.ToDurableTaskOptions()));
    }

    public override Task CreateTimer(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return this.innerContext.CreateTimer(delay, cancellationToken);
    }

    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        return this.innerContext.CreateTimer(fireAt, cancellationToken);
    }

    public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default)
    {
        return this.innerContext.WaitForExternalEvent<T>(eventName, cancellationToken);
    }

    public override Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout)
    {
        return this.innerContext.WaitForExternalEvent<T>(eventName, timeout);
    }

    public override void SendEvent(string instanceId, string eventName, object payload)
    {
        this.innerContext.SendEvent(instanceId, eventName, payload);
    }

    public override void SetCustomStatus(object? customStatus)
    {
        this.innerContext.SetCustomStatus(customStatus);
    }

    public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null)
    {
        return WrapExceptions(this.innerContext.CallSubOrchestratorAsync<TResult>(workflowName, input, options?.ToDurableTaskOptions()));
    }

    public override Task CallChildWorkflowAsync(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null)
    {
        return WrapExceptions(this.innerContext.CallSubOrchestratorAsync(workflowName, input, options?.ToDurableTaskOptions()));
    }

    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
    {
        this.innerContext.ContinueAsNew(newInput!, preserveUnprocessedEvents);
    }

    public override Guid NewGuid()
    {
        return this.innerContext.NewGuid();
    }

    /// <summary>
    /// Returns an instance of <see cref="ILogger"/> that is replay-safe, meaning that the logger only
    /// writes logs when the orchestrator is not replaying previous history.
    /// </summary>
    /// <param name="categoryName">The logger's category name.</param>
    /// <returns>An instance of <see cref="ILogger"/> that is replay-safe.</returns>
    public override ILogger CreateReplaySafeLogger(string categoryName) =>
        this.innerContext.CreateReplaySafeLogger(categoryName);

    /// <inheritdoc cref="CreateReplaySafeLogger(string)" />
    /// <param name="type">The type to derive the category name from.</param>
    public override ILogger CreateReplaySafeLogger(Type type) =>
        this.innerContext.CreateReplaySafeLogger(type);

    /// <inheritdoc cref="CreateReplaySafeLogger(string)" />
    /// <typeparam name="T">The type to derive category name from.</typeparam>
    public override ILogger CreateReplaySafeLogger<T>() =>
        this.innerContext.CreateReplaySafeLogger<T>();

    static async Task WrapExceptions(Task task)
    {
        try
        {
            await task;
        }
        catch (TaskFailedException ex)
        {
            var details = new WorkflowTaskFailureDetails(ex.FailureDetails);
            throw new WorkflowTaskFailedException(ex.Message, details);
        }
    }

    static async Task<TResult> WrapExceptions<TResult>(Task<TResult> task)
    {
        try
        {
            return await task;
        }
        catch (TaskFailedException ex)
        {
            var details = new WorkflowTaskFailureDetails(ex.FailureDetails);
            throw new WorkflowTaskFailedException(ex.Message, details);
        }
    }
}