using System.Threading.Tasks;
using Dapr.Workflow;
using Moq;
using WorkflowConsoleApp;
using WorkflowConsoleApp.Activities;
using WorkflowConsoleApp.Workflows;
using Xunit;

namespace WorkflowUnitTest;

[Trait("Example", "true")]
public class OrderProcessingTests
{
    [Fact]
    public async Task TestSuccessfulOrder()
    {
        // Test payloads
        OrderPayload order = new(Name: "Paperclips", TotalCost: 99.95, Quantity: 10);
        PaymentRequest expectedPaymentRequest = new(It.IsAny<string>(), order.Name, order.Quantity, order.TotalCost);
        InventoryRequest expectedInventoryRequest = new(It.IsAny<string>(), order.Name, order.Quantity);
        InventoryResult inventoryResult = new(Success: true, new InventoryItem(order.Name, 9.99, order.Quantity));

        // Mock the call to ReserveInventoryActivity
        Mock<WorkflowContext> mockContext = new();
        mockContext
            .Setup(ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), It.IsAny<InventoryRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.FromResult(inventoryResult));

        // Run the workflow directly
        OrderResult result = await new OrderProcessingWorkflow().RunAsync(mockContext.Object, order);
            
        // Verify that workflow result matches what we expect
        Assert.NotNull(result);
        Assert.True(result.Processed);

        // Verify that ReserveInventoryActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), expectedInventoryRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that ProcessPaymentActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(ProcessPaymentActivity), expectedPaymentRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that there were two calls to NotifyActivity
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(NotifyActivity), It.IsAny<Notification>(), It.IsAny<WorkflowTaskOptions>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TestInsufficientInventory()
    {
        // Test payloads
        OrderPayload order = new(Name: "Paperclips", TotalCost: 99.95, Quantity: int.MaxValue);
        InventoryRequest expectedInventoryRequest = new(It.IsAny<string>(), order.Name, order.Quantity);
        InventoryResult inventoryResult = new(Success: false, null);

        // Mock the call to ReserveInventoryActivity
        Mock<WorkflowContext> mockContext = new();
        mockContext
            .Setup(ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), It.IsAny<InventoryRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.FromResult(inventoryResult));

        // Run the workflow directly
        await new OrderProcessingWorkflow().RunAsync(mockContext.Object, order);

        // Verify that ReserveInventoryActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), expectedInventoryRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that ProcessPaymentActivity was never called
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(ProcessPaymentActivity), It.IsAny<PaymentRequest>(), It.IsAny<WorkflowTaskOptions>()),
            Times.Never());

        // Verify that there were two calls to NotifyActivity
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(NotifyActivity), It.IsAny<Notification>(), It.IsAny<WorkflowTaskOptions>()),
            Times.Exactly(2));
    }
}