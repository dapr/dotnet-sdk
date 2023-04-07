using Dapr.Workflow;
using DurableTask.Core.Exceptions;
using WorkflowConsoleApp.Activities;
using WorkflowConsoleApp.Models;

namespace WorkflowConsoleApp.Workflows
{
    public class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
    {
        public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
        {

            var retryPolicy = Microsoft.DurableTask.TaskOptions.FromRetryPolicy(new Microsoft.DurableTask.RetryPolicy(
                maxNumberOfAttempts: 10,
                firstRetryInterval: TimeSpan.FromMinutes(1),
                backoffCoefficient: 2.0,
                maxRetryInterval: TimeSpan.FromHours(1)));


            string orderId = context.InstanceId;

            // Notify the user that an order has come through
            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Received order {orderId} for {order.Quantity} {order.Name} at ${order.TotalCost}"), retryPolicy);

            // Since company cars are expensive and exclusive, we want to authenticate before allowing the purchase
            if (order.Name == "companycar")
            {
                InventoryResult authResult = await context.CallActivityAsync<InventoryResult>(
                    nameof(AuthenticationActivity),
                    new InventoryRequest(RequestId: orderId, order.Name, order.Quantity), retryPolicy);
                if (!authResult.Success)
                {
                // End the workflow here since we don't have sufficient inventory
                await context.CallActivityAsync(
                    nameof(NotifyActivity),
                    new Notification($"Authentication Failed for {order.Name}"), retryPolicy);
                return new OrderResult(Processed: false);
                }
            }

            // Determine if there is enough of the item available for purchase by checking the inventory
            InventoryResult result = await context.CallActivityAsync<InventoryResult>(
                nameof(ReserveInventoryActivity),
                new InventoryRequest(RequestId: orderId, order.Name, order.Quantity));
            
            // If there is insufficient inventory, fail and let the user know 
            if (!result.Success)
            {
                // End the workflow here since we don't have sufficient inventory
                await context.CallActivityAsync(
                    nameof(NotifyActivity),
                    new Notification($"Insufficient inventory for {order.Name}"), retryPolicy);
                return new OrderResult(Processed: false);
            }

            // There is enough inventory available so the user can purchase the item(s). Process their payment
            await context.CallActivityAsync(
                nameof(ProcessPaymentActivity),
                new PaymentRequest(RequestId: orderId, order.Name, order.Quantity, order.TotalCost), retryPolicy);

            try
            {
                // There is enough inventory available so the user can purchase the item(s). Process their payment
                await context.CallActivityAsync(
                    nameof(UpdateInventoryActivity),
                    new PaymentRequest(RequestId: orderId, order.Name, order.Quantity, order.TotalCost), retryPolicy);                
            }
            catch (TaskFailedException)
            {
                // Let them know their payment was processed
                await context.CallActivityAsync(
                    nameof(NotifyActivity),
                    new Notification($"Order {orderId} Failed! You are now getting a refund"), retryPolicy);
                return new OrderResult(Processed: false);
            }

            // Let them know their payment was processed
            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Order {orderId} has completed!"), retryPolicy);

            // End the workflow with a success result
            return new OrderResult(Processed: true);
        }
    }
}
