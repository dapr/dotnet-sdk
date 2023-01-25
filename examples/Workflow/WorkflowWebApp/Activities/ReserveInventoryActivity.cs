﻿namespace WorkflowWebApp.Activities
{
    using System.Threading.Tasks;
    using Dapr.Workflow;

    record InventoryRequest(string RequestId, string Name, int Quantity);
    record InventoryResult(bool Success);

    class ReserveInventoryActivity : WorkflowActivity<InventoryRequest, InventoryResult>
    {
        readonly ILogger logger;

        public ReserveInventoryActivity(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<ReserveInventoryActivity>();
        }

        public override async Task<InventoryResult> RunAsync(WorkflowActivityContext context, InventoryRequest req)
        {
            this.logger.LogInformation(
                "Reserving inventory: {requestId}, {name}, {quantity}",
                req.RequestId,
                req.Name,
                req.Quantity);

            // Simulate slow processing
            await Task.Delay(TimeSpan.FromSeconds(2));

            return new InventoryResult(true);
        }
    }
}
