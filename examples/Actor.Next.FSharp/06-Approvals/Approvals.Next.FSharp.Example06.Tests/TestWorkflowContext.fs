namespace Approvals.Next.FSharp.Example06.Tests

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Dapr.Workflow

type TestWorkflowContext() =
    inherit WorkflowContext()

    let activityCalls = ResizeArray<string * obj>()
    let activityResults = Dictionary<string, obj -> Task>()

    member _.SetActivityResult(name: string, result: obj -> Task) =
        activityResults.[name] <- result

    member _.GetCallCount(name: string) =
        activityCalls |> Seq.filter (fun (n, _) -> n = name) |> Seq.length

    member _.GetCalls<'T>(name: string) =
        activityCalls
        |> Seq.filter (fun (n, _) -> n = name)
        |> Seq.map (fun (_, input) -> input :?> 'T)
        |> Seq.toList

    override _.Name = "test"
    override _.InstanceId = "test-instance"
    override _.CurrentUtcDateTime = DateTime.UtcNow
    override _.IsReplaying = false
    override _.IsPatched(patchName: string) = false

    override _.CallActivityAsync(name: string, input: obj, options: WorkflowTaskOptions) : Task =
        activityCalls.Add((name, input))
        match activityResults.TryGetValue(name) with
        | true, fn -> fn(input)
        | false, _ -> Task.CompletedTask

    override _.CallActivityAsync<'T>(name: string, input: obj, options: WorkflowTaskOptions) : Task<'T> =
        raise (NotImplementedException())

    override _.CreateTimer(fireAt: DateTime, cancellationToken: CancellationToken) : Task =
        raise (NotImplementedException())

    override _.WaitForExternalEventAsync<'T>(eventName: string, cancellationToken: CancellationToken) : Task<'T> =
        raise (NotImplementedException())

    override _.SendEvent(instanceId: string, eventName: string, payload: obj) : unit =
        raise (NotImplementedException())

    override _.SetCustomStatus(customStatus: obj) : unit =
        raise (NotImplementedException())

    override _.CallChildWorkflowAsync<'TResult>(workflowName: string, input: obj, options: ChildWorkflowTaskOptions) : Task<'TResult> =
        raise (NotImplementedException())

    override _.CreateReplaySafeLogger(categoryName: string) : ILogger =
        NullLogger.Instance :> ILogger

    override _.CreateReplaySafeLogger(``type``: Type) : ILogger =
        NullLogger.Instance :> ILogger

    override _.CreateReplaySafeLogger<'T>() : ILogger =
        NullLogger<'T>.Instance :> ILogger

    override _.ContinueAsNew(newInput: obj, preserveUnprocessedEvents: bool) : unit =
        raise (NotImplementedException())

    override _.NewGuid() : Guid = Guid.NewGuid()

    override _.GetPropagatedHistory() : PropagatedHistory =
        null
