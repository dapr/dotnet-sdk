using Dapr.Workflow;
using WorkflowConsoleApp.Activities;
using WorkflowConsoleApp.Models;

namespace WorkflowConsoleApp.Workflows
{
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
                // End the workflow here since we don't have sufficient inventory
                await context.CallActivityAsync(
                    nameof(NotifyActivity),
                    new Notification($"Insufficient inventory for {order.Name}"));
                return new OrderResult(Processed: false);
            }

            // There is enough inventory available so the user can purchase the item(s). Process their payment
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
                await context.CallActivityAsync(
                    nameof(NotifyActivity),
                    new Notification($"Order {orderId} Failed! Details: {e.FailureDetails.ErrorMessage}"));
                return new OrderResult(Processed: false);
            }

            // Let them know their payment was processed
            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Order {orderId} has completed!"));

            // End the workflow with a success result
            return new OrderResult(Processed: true);
        }
    }
}
