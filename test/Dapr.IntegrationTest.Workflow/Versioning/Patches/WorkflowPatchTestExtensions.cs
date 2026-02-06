using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Workflow;

namespace Dapr.IntegrationTest.Workflow.Versioning.Patches;

internal static class WorkflowPatchTestExtensions
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public static async Task<WorkflowState> WaitForWorkflowCompletionOrStalledAsync(
        this DaprWorkflowClient client,
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        while (true)
        {
            var state = await client.GetWorkflowStateAsync(instanceId, getInputsAndOutputs, cancellation);

            if (state is null)
            {
                throw new InvalidOperationException($"Workflow instance '{instanceId}' does not exist");
            }

            if (IsTerminalOrStalled(state.RuntimeStatus))
            {
                return state;
            }

            await Task.Delay(PollInterval, cancellation);
        }
    }

    private static bool IsTerminalOrStalled(WorkflowRuntimeStatus status) =>
        status is WorkflowRuntimeStatus.Completed
            or WorkflowRuntimeStatus.Failed
            or WorkflowRuntimeStatus.Terminated
            or WorkflowRuntimeStatus.Stalled;
}
