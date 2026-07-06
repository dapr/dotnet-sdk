using System;
using System.Threading.Tasks;
using Dapr.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
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
        mockContext
            .Setup(ctx => ctx.CreateReplaySafeLogger<OrderProcessingWorkflow>())
            .Returns(NullLogger<OrderProcessingWorkflow>.Instance);

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
    public async Task TestHighCostOrderApproved()
    {
        // Test payloads
        OrderPayload order = new(Name: "Cars", TotalCost: 100000, Quantity: 2);
        InventoryResult inventoryResult = new(Success: true, null);
        PaymentRequest expectedPaymentRequest = new(It.IsAny<string>(), order.Name, order.Quantity, order.TotalCost);
        InventoryRequest expectedInventoryRequest = new(It.IsAny<string>(), order.Name, order.Quantity);

        // Mock the call to ReserveInventoryActivity with a total cost exceeding the approval threshold
        Mock<WorkflowContext> mockContext = new();
        mockContext
            .Setup(ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), It.IsAny<InventoryRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.FromResult(inventoryResult));
        // Approve any approval requests
        mockContext
            .Setup(ctx => ctx.WaitForExternalEventAsync<ApprovalResult>(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.FromResult(ApprovalResult.Approved));
        mockContext
            .Setup(ctx => ctx.CreateReplaySafeLogger<OrderProcessingWorkflow>())
            .Returns(NullLogger<OrderProcessingWorkflow>.Instance);

        // Run the workflow directly
        OrderResult result = await new OrderProcessingWorkflow().RunAsync(mockContext.Object, order);

        // Verify that workflow result matches what we expect
        Assert.NotNull(result);
        Assert.True(result.Processed);

        // Verify that ReserveInventoryActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), expectedInventoryRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that RequestApprovalActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(RequestApprovalActivity), order, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that the Approval Request was called with a specific input
        mockContext.Verify(
            ctx => ctx.WaitForExternalEventAsync<ApprovalResult>("ManagerApproval", TimeSpan.FromSeconds(30)),
            Times.Once());

        // Verify that the Custom Status was set with a specific message
        mockContext.Verify(
            ctx => ctx.SetCustomStatus("Waiting for approval"),
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
    public async Task TestHighCostOrderApprovalTimeout()
    {
        // Test payloads
        OrderPayload order = new(Name: "Cars", TotalCost: 100000, Quantity: 2);
        InventoryResult inventoryResult = new(Success: true, null);
        InventoryRequest expectedInventoryRequest = new(It.IsAny<string>(), order.Name, order.Quantity);

        Mock<WorkflowContext> mockContext = new();
        // Mock the call to ReserveInventoryActivity
        mockContext
            .Setup(ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), It.IsAny<InventoryRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.FromResult(inventoryResult));
        // Mock a timeout after waiting for approval
        mockContext
            .Setup(ctx => ctx.WaitForExternalEventAsync<ApprovalResult>(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.FromException<ApprovalResult>(new TaskCanceledException()));
        mockContext
            .Setup(ctx => ctx.CreateReplaySafeLogger<OrderProcessingWorkflow>())
            .Returns(NullLogger<OrderProcessingWorkflow>.Instance);

        // Run the workflow directly
        OrderResult result = await new OrderProcessingWorkflow().RunAsync(mockContext.Object, order);

        // Verify that workflow result matches what we expect (not processed)
        Assert.NotNull(result);
        Assert.False(result.Processed);

        // Verify that ReserveInventoryActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), expectedInventoryRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that ProcessPaymentActivity was not called
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(ProcessPaymentActivity), It.IsAny<PaymentRequest>(), It.IsAny<WorkflowTaskOptions>()),
            Times.Never());

        // Verify that UpdateInventoryActivity was not called
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(UpdateInventoryActivity), It.IsAny<PaymentRequest>(), It.IsAny<WorkflowTaskOptions>()),
            Times.Never());

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
        mockContext
            .Setup(ctx => ctx.CreateReplaySafeLogger<OrderProcessingWorkflow>())
            .Returns(NullLogger<OrderProcessingWorkflow>.Instance);

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

    [Fact]
    public async Task TestActivityException()
    {
        // Test payloads
        OrderPayload order = new(Name: "Paperclips", TotalCost: 99.95, Quantity: 10);
        PaymentRequest expectedPaymentRequest = new(It.IsAny<string>(), order.Name, order.Quantity, order.TotalCost);
        InventoryRequest expectedInventoryRequest = new(It.IsAny<string>(), order.Name, order.Quantity);
        InventoryResult inventoryResult = new(Success: true, null);

        Mock<WorkflowContext> mockContext = new();
        // Mock the call to ReserveInventoryActivity
        mockContext
            .Setup(ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), It.IsAny<InventoryRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.FromResult(inventoryResult));
        // Throws a WorkflowTaskFailedException on UpdateInventoryActivity
        mockContext
            .Setup(ctx => ctx.CallActivityAsync(nameof(UpdateInventoryActivity), It.IsAny<PaymentRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.FromException(new WorkflowTaskFailedException("fail", new WorkflowTaskFailureDetails("type", "message"))));
        mockContext
            .Setup(ctx => ctx.CreateReplaySafeLogger<OrderProcessingWorkflow>())
            .Returns(NullLogger<OrderProcessingWorkflow>.Instance);

        // Run the workflow directly
        OrderResult result = await new OrderProcessingWorkflow().RunAsync(mockContext.Object, order);

        // Verify that workflow result matches what we expect (not processed)
        Assert.NotNull(result);
        Assert.False(result.Processed);

        // Verify that ReserveInventoryActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), expectedInventoryRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that ProcessPaymentActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(ProcessPaymentActivity), expectedPaymentRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that UpdateInventoryActivity was called with a specific input
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(UpdateInventoryActivity), expectedPaymentRequest, It.IsAny<WorkflowTaskOptions>()),
            Times.Once());

        // Verify that there were two calls to NotifyActivity
        mockContext.Verify(
            ctx => ctx.CallActivityAsync(nameof(NotifyActivity), It.IsAny<Notification>(), It.IsAny<WorkflowTaskOptions>()),
            Times.Exactly(2));
    }
}
