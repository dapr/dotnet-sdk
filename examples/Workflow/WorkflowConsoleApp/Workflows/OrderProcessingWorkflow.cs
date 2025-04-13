using Dapr.Workflow;
using Microsoft.Extensions.Logging;
using WorkflowConsoleApp.Activities;

namespace WorkflowConsoleApp.Workflows;

public class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
{
    readonly WorkflowTaskOptions defaultActivityRetryOptions = new WorkflowTaskOptions
    {
        // NOTE: Beware that changing the number of retries is a breaking change for existing workflows.
        RetryPolicy = new WorkflowRetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5)),
    };

    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
    {
        string orderId = context.InstanceId;
        var logger = context.CreateReplaySafeLogger<OrderProcessingWorkflow>();

        logger.LogInformation("Received order {orderId} for {quantity} {name} at ${totalCost}", orderId, order.Quantity, order.Name, order.TotalCost);
            
        // Notify the user that an order has come through
        await context.CallActivityAsync(
            nameof(NotifyActivity),
            new Notification($"Received order {orderId} for {order.Quantity} {order.Name} at ${order.TotalCost}"));

        // Determine if there is enough of the item available for purchase by checking the inventory
        InventoryResult result = await context.CallActivityAsync<InventoryResult>(
            nameof(ReserveInventoryActivity),
            new InventoryRequest(RequestId: orderId, order.Name, order.Quantity),
            this.defaultActivityRetryOptions);
            
        // If there is insufficient inventory, fail and let the user know 
        if (!result.Success)
        {
            logger.LogError("Insufficient inventory for {orderName}", order.Name);
                
            // End the workflow here since we don't have sufficient inventory
            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Insufficient inventory for {order.Name}"));
            return new OrderResult(Processed: false);
        }

        // Require orders over a certain threshold to be approved
        const int threshold = 50000;
        if (order.TotalCost > threshold)
        {
            logger.LogInformation("Requesting manager approval since total cost {totalCost} exceeds threshold {threshold}", order.TotalCost, threshold);
            // Request manager approval for the order
            await context.CallActivityAsync(nameof(RequestApprovalActivity), order);

            try
            {
                // Pause and wait for a manager to approve the order
                context.SetCustomStatus("Waiting for approval");
                ApprovalResult approvalResult = await context.WaitForExternalEventAsync<ApprovalResult>(
                    eventName: "ManagerApproval",
                    timeout: TimeSpan.FromSeconds(30));
                    
                logger.LogInformation("Approval result: {approvalResult}", approvalResult);
                context.SetCustomStatus($"Approval result: {approvalResult}");
                if (approvalResult == ApprovalResult.Rejected)
                {
                    logger.LogWarning("Order was rejected by approver");
                        
                    // The order was rejected, end the workflow here
                    await context.CallActivityAsync(
                        nameof(NotifyActivity),
                        new Notification($"Order was rejected by approver"));
                    return new OrderResult(Processed: false);
                }
            }
            catch (TaskCanceledException)
            {
                logger.LogError("Cancelling order because it didn't receive an approval");
                    
                // An approval timeout results in automatic order cancellation
                await context.CallActivityAsync(
                    nameof(NotifyActivity),
                    new Notification($"Cancelling order because it didn't receive an approval"));
                return new OrderResult(Processed: false);
            }
        }

        // There is enough inventory available so the user can purchase the item(s). Process their payment
        logger.LogInformation("Processing payment as sufficient inventory is available");
        await context.CallActivityAsync(
            nameof(ProcessPaymentActivity),
            new PaymentRequest(RequestId: orderId, order.Name, order.Quantity, order.TotalCost),
            this.defaultActivityRetryOptions);

        try
        {
            // There is enough inventory available so the user can purchase the item(s). Process their payment
            await context.CallActivityAsync(
                nameof(UpdateInventoryActivity),
                new PaymentRequest(RequestId: orderId, order.Name, order.Quantity, order.TotalCost),
                this.defaultActivityRetryOptions);                
        }
        catch (WorkflowTaskFailedException e)
        {
            // Let them know their payment processing failed
            logger.LogError("Order {orderId} failed! Details: {errorMessage}", orderId, e.FailureDetails.ErrorMessage);
            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Order {orderId} Failed! Details: {e.FailureDetails.ErrorMessage}"));
            return new OrderResult(Processed: false);
        }

        // Let them know their payment was processed
        logger.LogError("Order {orderId} has completed!", orderId);
        await context.CallActivityAsync(
            nameof(NotifyActivity),
            new Notification($"Order {orderId} has completed!"));

        // End the workflow with a success result
        return new OrderResult(Processed: true);
    }
}