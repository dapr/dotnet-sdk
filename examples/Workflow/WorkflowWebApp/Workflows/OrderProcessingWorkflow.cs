namespace WorkflowWebApp.Workflows
{
    using System.Threading.Tasks;
    using Dapr.Workflow;
    using WorkflowWebApp.Activities;

    record OrderPayload(string Name, double TotalCost, int Quantity = 1);
    record OrderResult(bool Processed);

    class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
    {
        public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
        {
            string orderId = context.InstanceId;

            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Received order {orderId} for {order.Name} at {order.TotalCost:c}"));

            string requestId = context.InstanceId;

            InventoryResult result = await context.CallActivityAsync<InventoryResult>(
                nameof(ReserveInventoryActivity),
                new InventoryRequest(RequestId: orderId, order.Name, order.Quantity));
            if (!result.Success)
            {
                // End the workflow here since we don't have sufficient inventory
                context.SetCustomStatus($"Insufficient inventory for {order.Name}");
                return new OrderResult(Processed: false);
            }

            await context.CallActivityAsync(
                nameof(ProcessPaymentActivity),
                new PaymentRequest(RequestId: orderId, order.TotalCost, "USD"));

            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Order {orderId} processed successfully!"));

            // End the workflow with a success result
            return new OrderResult(Processed: true);
        }
    }
}
