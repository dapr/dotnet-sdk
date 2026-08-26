#nowarn "FS3261"
namespace WorkflowVersioning.FSharp.Workflows.VacationApproval

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Dapr.Workflow
open WorkflowVersioning.FSharp.Workflows.VacationApproval.Activities
open WorkflowVersioning.FSharp.Workflows.VacationApproval.Models

type VacationApprovalWorkflow() =
    inherit Workflow<VacationRequest, bool>()

    override _.RunAsync(context: WorkflowContext, input: VacationRequest) : Task<bool> =
        task {
            let tooSoon =
                if context.IsPatched("needs-two-weeks-notice") then
                    let now = context.CurrentUtcDateTime
                    input.StartDate < DateOnly(now.Year, now.Month, now.Day).AddDays(14)
                else
                    false

            if tooSoon then
                return false
            else
                do! context.CallActivityAsync(
                    typeof<SendEmailActivity>.Name,
                    box ({ To = "manager@localhost"
                           Message = $"Vacation request '{context.InstanceId}' from {input.EmployeeName} from {input.StartDate:d} to {input.EndDate:d}" } : EmailActivityInput))

                let mutable timedOut = false
                try
                    let! _ = context.WaitForExternalEventAsync<bool>("Approval", timeout = TimeSpan.FromSeconds(120.0))
                    ()
                with :? TaskCanceledException ->
                    do! context.CallActivityAsync(
                        typeof<SendEmailActivity>.Name,
                        box ({ To = $"{input.EmployeeName}@localhost"
                               Message = $"Vacation request '{context.InstanceId}' denied from {input.StartDate:d} to {input.EndDate:d}" } : EmailActivityInput))
                    timedOut <- true

                if timedOut then
                    return false
                else
                    do! context.CallActivityAsync(
                        typeof<SendEmailActivity>.Name,
                        box ({ To = $"{input.EmployeeName}@localhost"
                               Message = $"Vacation request '{context.InstanceId}' approved from {input.StartDate:d} to {input.EndDate:d}" } : EmailActivityInput))
                    return true
        }


type VacationApprovalWorkflowV2() =
    inherit Workflow<VacationRequest, bool>()

    override _.RunAsync(context: WorkflowContext, input: VacationRequest) : Task<bool> =
        task {
            let logger = context.CreateReplaySafeLogger<VacationApprovalWorkflowV2>()

            let now = context.CurrentUtcDateTime
            if input.StartDate < DateOnly(now.Year, now.Month, now.Day).AddDays(14) then
                return false
            else
                logger.LogInformation("Sending approval email to manager for workflow '{workflowId}'", context.InstanceId)
                do! context.CallActivityAsync(
                    typeof<SendEmailActivity>.Name,
                    box ({ To = "manager@localhost"
                           Message = $"Vacation request '{context.InstanceId}' from {input.EmployeeName} from {input.StartDate:d} to {input.EndDate:d}" } : EmailActivityInput))

                let denialMessage = $"Vacation request '{context.InstanceId}' denied from {input.StartDate:d} to {input.EndDate:d}"
                let mutable timedOut = false
                let mutable approvalResponse = false

                try
                    logger.LogInformation("Waiting for approval for workflow '{workflowId}'", context.InstanceId)
                    let! ar = context.WaitForExternalEventAsync<bool>("Approval", timeout = TimeSpan.FromSeconds(120.0))
                    approvalResponse <- ar
                with :? TaskCanceledException ->
                    logger.LogWarning("Approval timeout for workflow '{workflowId}'", context.InstanceId)
                    do! context.CallActivityAsync(
                        typeof<SendEmailActivity>.Name,
                        box ({ To = $"{input.EmployeeName}@localhost"; Message = denialMessage } : EmailActivityInput))
                    timedOut <- true

                if timedOut then
                    return false
                else
                    let approvalMessage = $"Vacation request '{context.InstanceId}' approved from {input.StartDate:d} to {input.EndDate:d}"
                    logger.LogInformation(
                        "Received approval decision for workflow '{workflowId}', status: '{status}'",
                        context.InstanceId,
                        if approvalResponse then "Approved" else "Denied")
                    do! context.CallActivityAsync(
                        typeof<SendEmailActivity>.Name,
                        box ({ To = $"{input.EmployeeName}@localhost"
                               Message = if approvalResponse then approvalMessage else denialMessage } : EmailActivityInput))
                    return approvalResponse
        }