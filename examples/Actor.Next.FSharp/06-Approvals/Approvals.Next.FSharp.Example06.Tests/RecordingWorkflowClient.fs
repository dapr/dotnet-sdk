namespace Approvals.Next.FSharp.Example06.Tests

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Dapr.Workflow
open Dapr.Workflow.Client

type RecordingWorkflowClient() =
    let scheduled = ResizeArray<string>()

    member _.Scheduled : IReadOnlyList<string> =
        upcast scheduled

    interface IDaprWorkflowClient with
        member _.ScheduleNewWorkflowAsync(name: string, instanceId: string, input: obj) : Task<string> =
            let id = if isNull instanceId then Guid.NewGuid().ToString() else instanceId
            scheduled.Add(id)
            Task.FromResult(id)

        member _.ScheduleNewWorkflowAsync(name: string, instanceId: string, input: obj, startTime: Nullable<DateTime>) : Task<string> =
            raise (NotImplementedException())

        member _.ScheduleNewWorkflowAsync(name: string, instanceId: string, input: obj, startTime: Nullable<DateTimeOffset>, cancellation: CancellationToken) : Task<string> =
            raise (NotImplementedException())

        member _.GetWorkflowStateAsync(instanceId: string, getInputsAndOutputs: bool, cancellation: CancellationToken) : Task<WorkflowState> =
            raise (NotImplementedException())

        member _.WaitForWorkflowStartAsync(instanceId: string, getInputsAndOutputs: bool, cancellation: CancellationToken) : Task<WorkflowState> =
            raise (NotImplementedException())

        member _.WaitForWorkflowCompletionAsync(instanceId: string, getInputsAndOutputs: bool, cancellation: CancellationToken) : Task<WorkflowState> =
            raise (NotImplementedException())

        member _.RaiseEventAsync(instanceId: string, eventName: string, eventPayload: obj, cancellation: CancellationToken) : Task =
            raise (NotImplementedException())

        member _.TerminateWorkflowAsync(instanceId: string, output: obj, cancellation: CancellationToken) : Task =
            raise (NotImplementedException())

        member _.SuspendWorkflowAsync(instanceId: string, reason: string, cancellation: CancellationToken) : Task =
            raise (NotImplementedException())

        member _.ResumeWorkflowAsync(instanceId: string, reason: string, cancellation: CancellationToken) : Task =
            raise (NotImplementedException())

        member _.PurgeInstanceAsync(instanceId: string, cancellation: CancellationToken) : Task<bool> =
            raise (NotImplementedException())

        member _.ListInstanceIdsAsync(continuationToken: string, pageSize: Nullable<int>, cancellation: CancellationToken) : Task<WorkflowInstancePage> =
            raise (NotImplementedException())

        member _.GetInstanceHistoryAsync(instanceId: string, cancellation: CancellationToken) : Task<IReadOnlyList<WorkflowHistoryEvent>> =
            raise (NotImplementedException())

        member _.RerunWorkflowFromEventAsync(sourceInstanceId: string, eventId: uint32, options: RerunWorkflowFromEventOptions, cancellation: CancellationToken) : Task<string> =
            raise (NotImplementedException())

    interface IDisposable with
        member _.Dispose() = ()

    interface IAsyncDisposable with
        member _.DisposeAsync() : ValueTask = ValueTask.CompletedTask
