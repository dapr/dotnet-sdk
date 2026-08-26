#nowarn "FS3261"
namespace WorkflowConsoleApp.Workflows

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Dapr.Workflow
open WorkflowConsoleApp
open WorkflowConsoleApp.Activities

type OrderProcessingWorkflow() =
    inherit Workflow<OrderPayload, OrderResult>()

    static member DefaultRetryOptions =
        WorkflowTaskOptions(
            RetryPolicy = WorkflowRetryPolicy(
                maxNumberOfAttempts = 3,
                firstRetryInterval = TimeSpan.FromSeconds(5.0)))

    override _.RunAsync(context: WorkflowContext, order: OrderPayload) : Task<OrderResult> =
        task {
            let orderId = context.InstanceId
            let logger = context.CreateReplaySafeLogger<OrderProcessingWorkflow>()

            logger.LogInformation(
                "Received order {orderId} for {quantity} {name} at ${totalCost}",
                orderId, order.Quantity, order.Name, order.TotalCost)

            do! context.CallActivityAsync(
                typeof<NotifyActivity>.Name,
                { Message = $"Received order {orderId} for {order.Quantity} {order.Name} at ${order.TotalCost}" })

            let! result = context.CallActivityAsync<InventoryResult>(
                typeof<ReserveInventoryActivity>.Name,
                { RequestId = orderId; ItemName = order.Name; Quantity = order.Quantity },
                OrderProcessingWorkflow.DefaultRetryOptions)

            if not result.Success then
                logger.LogError("Insufficient inventory for {orderName}", order.Name)
                do! context.CallActivityAsync(
                    typeof<NotifyActivity>.Name,
                    { Message = $"Insufficient inventory for {order.Name}" })
                return { Processed = false }
            else
                let! approvalOutcome =
                    task {
                        let threshold = 50000
                        if order.TotalCost > threshold then
                            logger.LogInformation(
                                "Requesting manager approval since total cost {totalCost} exceeds threshold {threshold}",
                                order.TotalCost, threshold)
                            do! context.CallActivityAsync(typeof<RequestApprovalActivity>.Name, order)
                            context.SetCustomStatus("Waiting for approval")
                            try
                                let! approvalResult = context.WaitForExternalEventAsync<ApprovalResult>(
                                    eventName = "ManagerApproval",
                                    timeout = TimeSpan.FromSeconds(30.0))
                                logger.LogInformation("Approval result: {approvalResult}", approvalResult)
                                context.SetCustomStatus($"Approval result: {approvalResult}")
                                if approvalResult = ApprovalResult.Rejected then
                                    logger.LogWarning("Order was rejected by approver")
                                    do! context.CallActivityAsync(
                                        typeof<NotifyActivity>.Name,
                                        { Message = "Order was rejected by approver" })
                                    return Some { Processed = false }
                                else
                                    return None
                            with :? TaskCanceledException ->
                                logger.LogError("Cancelling order because it didn't receive an approval")
                                do! context.CallActivityAsync(
                                    typeof<NotifyActivity>.Name,
                                    { Message = "Cancelling order because it didn't receive an approval" })
                                return Some { Processed = false }
                        else
                            return None
                    }

                match approvalOutcome with
                | Some outcome ->
                    return outcome
                | None ->
                    logger.LogInformation("Processing payment as sufficient inventory is available")
                    do! context.CallActivityAsync(
                        typeof<ProcessPaymentActivity>.Name,
                        { RequestId = orderId; ItemName = order.Name; Amount = order.Quantity; Currency = order.TotalCost },
                        OrderProcessingWorkflow.DefaultRetryOptions)

                    let mutable failed = false
                    try
                        do! context.CallActivityAsync(
                            typeof<UpdateInventoryActivity>.Name,
                            { RequestId = orderId; ItemName = order.Name; Amount = order.Quantity; Currency = order.TotalCost },
                            OrderProcessingWorkflow.DefaultRetryOptions)
                    with :? WorkflowTaskFailedException as e ->
                        logger.LogError("Order {orderId} failed! Details: {errorMessage}", orderId, e.FailureDetails.ErrorMessage)
                        do! context.CallActivityAsync(
                            typeof<NotifyActivity>.Name,
                            { Message = $"Order {orderId} Failed! Details: {e.FailureDetails.ErrorMessage}" })
                        failed <- true

                    if failed then
                        return { Processed = false }
                    else
                        logger.LogInformation("Order {orderId} has completed!", orderId)
                        do! context.CallActivityAsync(
                            typeof<NotifyActivity>.Name,
                            { Message = $"Order {orderId} has completed!" })
                        return { Processed = true }
        }