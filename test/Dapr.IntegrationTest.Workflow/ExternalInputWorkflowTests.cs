// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Client;
using Dapr.TestContainers.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.IntegrationTest.Workflow;

public sealed partial class ExternalInputWorkflowTests
{
    private readonly List<InventoryItem> BaseInventory =
    [
        new("Paperclips", 5, 100),
        new("Cars", 15000, 100),
        new("Computers", 500, 100)
    ];

    [Fact]
    public async Task ShouldHandleMultipleExternalEvents_Simple()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<MultiEventWorkflow>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(MultiEventWorkflow), workflowInstanceId);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Raise multiple events
        await daprWorkflowClient.RaiseEventAsync(workflowInstanceId, "Event1", "FirstData");
        await daprWorkflowClient.RaiseEventAsync(workflowInstanceId, "Event2", 42);
        await daprWorkflowClient.RaiseEventAsync(workflowInstanceId, "Event3", true);

        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal("FirstData-42-True", output);
    }

    [Fact]
    public async Task ShouldHandleStandardWorkflowsWithDependencyInjection()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                // Register the DaprClient for state management purposes
                builder.Services.AddDaprClient((sp, b) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var httpEndpoint = config["DAPR_HTTP_ENDPOINT"];
                    if (!string.IsNullOrEmpty(httpEndpoint))
                        b.UseHttpEndpoint(httpEndpoint);
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        b.UseGrpcEndpoint(grpcEndpoint);
                });

                // Register the Dapr Workflow client
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<OrderProcessingWorkflow>();
                        opt.RegisterActivity<UpdateInventoryActivity>();
                        opt.RegisterActivity<ProcessPaymentActivity>();
                        opt.RegisterActivity<ReserveInventoryActivity>();
                        opt.RegisterActivity<RequestApprovalActivity>();
                        opt.RegisterActivity<NotifyActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        // Clean test logic
        using var scope = testApp.CreateScope();

        // Set up the base inventory in the Dapr state management store
        var daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();
        foreach (var baseInventoryItem in BaseInventory)
        {
            await daprClient.SaveStateAsync(TestContainers.Constants.DaprComponentNames.StateManagementComponentName,
                baseInventoryItem.Name.ToLowerInvariant(), baseInventoryItem);
        }

        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        // Create an order under the threshold
        const string itemName = "Computers";
        const int amount = 3;
        var item = BaseInventory.First(item =>
            string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase));
        var totalCost = amount * item.PerItemCost;
        var orderInfo = new OrderPayload(itemName.ToLowerInvariant(), totalCost, amount);

        // Start the workflow
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), workflowInstanceId,
            orderInfo);


        // Wait for the workflow to complete - it shouldn't ask for approval
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var resultValue = result.ReadOutputAs<OrderResult>();
        Assert.NotNull(resultValue);
        Assert.True(resultValue.Processed);
    }

    [Fact]
    public async Task ShouldHandleExternalEventTimeout()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<TimeoutWorkflow>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TimeoutWorkflow), workflowInstanceId);

        // Don't send event, let it timeout
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal("Timeout", output);
    }

    [Fact]
    public async Task ShouldHandleExternalEventWithDefaultValue()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<DefaultValueWorkflow>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(DefaultValueWorkflow), workflowInstanceId);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<int>();
        Assert.Equal(42, output); // Default value
    }

    private sealed class TimeoutWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            try
            {
                await context.WaitForExternalEventAsync<string>("ApprovalEvent", TimeSpan.FromSeconds(5));
                return "Received";
            }
            catch (TaskCanceledException)
            {
                return "Timeout";
            }
        }
    }

    private sealed class DefaultValueWorkflow : Workflow<object?, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, object? input)
        {
            try
            {
                var value = await context.WaitForExternalEventAsync<int>("ValueEvent", TimeSpan.FromSeconds(5));
                return value;
            }
            catch (TaskCanceledException)
            {
                return 42; // Default value
            }
        }
    }

    public sealed record Notification(string Message);

    public sealed record OrderPayload(string Name, double TotalCost, int Quantity);

    public sealed record InventoryRequest(string RequestId, string ItemName, int Quantity);

    public sealed record InventoryResult(bool Success, InventoryItem? Item);

    public sealed record PaymentRequest(string RequestId, string ItemName, int Amount, double Currency);

    public sealed record InventoryItem(string Name, double PerItemCost, int Quantity);

    public sealed record OrderResult(bool Processed);

    public enum ApprovalResult
    {
        Unspecified = 0,
        Approved = 1,
        Rejected = 2
    }

    [Fact]
    public async Task ShouldHandleMultipleExternalEvents()
    {
        var options = new DaprRuntimeOptions();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<MultiEventWorkflow>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(MultiEventWorkflow), workflowInstanceId);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Raise multiple events
        await daprWorkflowClient.RaiseEventAsync(workflowInstanceId, "Event1", "FirstData");
        await daprWorkflowClient.RaiseEventAsync(workflowInstanceId, "Event2", 42);
        await daprWorkflowClient.RaiseEventAsync(workflowInstanceId, "Event3", true);

        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal("FirstData-42-True", output);
    }

    private sealed class MultiEventWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            var event1 = await context.WaitForExternalEventAsync<string>("Event1");
            var event2 = await context.WaitForExternalEventAsync<int>("Event2");
            var event3 = await context.WaitForExternalEventAsync<bool>("Event3");

            return $"{event1}-{event2}-{event3}";
        }
    }

    internal sealed partial class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
    {
        readonly WorkflowTaskOptions defaultActivityRetryOptions = new()
        {
            // NOTE: Beware that changing the number of retries is a breaking change for existing workflows.
            RetryPolicy = new WorkflowRetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(5)),
        };

        public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
        {
            var orderId = context.InstanceId;
            var logger = context.CreateReplaySafeLogger<OrderProcessingWorkflow>();

            LogReceivedOrder(logger, orderId, order.Quantity, order.Name, order.TotalCost);

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
                LogInsufficientInventory(logger, order.Name);

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
                LogRequestingApproval(logger, order.TotalCost, threshold);
                // Request manager approval for the order
                await context.CallActivityAsync(nameof(RequestApprovalActivity), order);

                try
                {
                    // Pause and wait for a manager to approve the order
                    context.SetCustomStatus("Waiting for approval");
                    ApprovalResult approvalResult = await context.WaitForExternalEventAsync<ApprovalResult>(
                        eventName: "ManagerApproval",
                        timeout: TimeSpan.FromSeconds(30));

                    LogApprovalResult(logger, approvalResult);
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
                    LogCancelingOrder(logger);

                    // An approval timeout results in automatic order cancellation
                    await context.CallActivityAsync(
                        nameof(NotifyActivity),
                        new Notification($"Cancelling order because it didn't receive an approval"));
                    return new OrderResult(Processed: false);
                }
            }

            // There is enough inventory available so the user can purchase the item(s). Process their payment
            LogInsufficientInventory(logger, order.Name);
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
                LogOrderFailed(logger, orderId, e.FailureDetails.ErrorMessage);
                await context.CallActivityAsync(
                    nameof(NotifyActivity),
                    new Notification($"Order {orderId} Failed! Details: {e.FailureDetails.ErrorMessage}"));
                return new OrderResult(Processed: false);
            }

            // Let them know their payment was processed
            LogOrderCompleted(logger, orderId);
            await context.CallActivityAsync(
                nameof(NotifyActivity),
                new Notification($"Order {orderId} has completed!"));

            // End the workflow with a success result
            return new OrderResult(Processed: true);
        }

        [LoggerMessage(LogLevel.Information, "Received order '{OrderId}' for {Quantity} of {ItemName} at ${TotalCost}")]
        private static partial void LogReceivedOrder(ILogger logger, string orderId, int quantity, string itemName,
            double totalCost);

        [LoggerMessage(LogLevel.Error, "Insufficient inventory for '{OrderName}'")]
        private static partial void LogInsufficientInventory(ILogger logger, string orderName);

        [LoggerMessage(LogLevel.Information,
            "Requesting manager approval since total cost {TotalCost} exceeds threshold {Threshold}")]
        private static partial void LogRequestingApproval(ILogger logger, double totalCost, double threshold);

        [LoggerMessage(LogLevel.Information, "Approval result: {ApprovalResult}")]
        private static partial void LogApprovalResult(ILogger logger, ApprovalResult approvalResult);

        [LoggerMessage(LogLevel.Information, "Processing payment as sufficient inventory is available")]
        private static partial void LogProcessingPayment(ILogger logger);

        [LoggerMessage(LogLevel.Error, "Cancelling order because it didn't receive an approval")]
        private static partial void LogCancelingOrder(ILogger logger);

        [LoggerMessage(LogLevel.Error, "Order {OrderId} failed! Details: {ErrorMessage}")]
        private static partial void LogOrderFailed(ILogger logger, string orderId, string errorMessage);

        [LoggerMessage(LogLevel.Information, "Order {OrderId} has completed!")]
        private static partial void LogOrderCompleted(ILogger logger, string orderId);
    }

    public sealed partial class UpdateInventoryActivity(ILogger<UpdateInventoryActivity> logger, DaprClient daprClient)
        : WorkflowActivity<PaymentRequest, object?>
    {
        public override async Task<object?> RunAsync(WorkflowActivityContext context, PaymentRequest request)
        {
            LogInventoryCheck(request.RequestId, request.Amount, request.ItemName);

            // Simulate slow processing
            await Task.Delay((TimeSpan.FromSeconds(5)));

            // Determine if there are enough items for purchase
            var item = await daprClient.GetStateAsync<InventoryItem>(
                TestContainers.Constants.DaprComponentNames.StateManagementComponentName,
                request.ItemName.ToLowerInvariant());
            var newQuantity = item.Quantity - request.Amount;
            if (newQuantity < 0)
            {
                LogInsufficientInventory(request.RequestId, request.Amount, item.Quantity, request.ItemName);
                throw new InvalidOperationException(
                    $"Not enough '{request.ItemName}' inventory! Requested {request.Amount} but only {item.Quantity} available.");
            }

            // Update the state store with the new amount of the item
            await daprClient.SaveStateAsync(TestContainers.Constants.DaprComponentNames.StateManagementComponentName,
                request.ItemName.ToLowerInvariant(),
                new InventoryItem(request.ItemName, item.PerItemCost, newQuantity));

            LogUpdatedInventory(newQuantity, item.Name);
            return null;
        }

        [LoggerMessage(LogLevel.Information, "Checking inventory for order '{RequestId}' for {Amount} {ItemName}")]
        private partial void LogInventoryCheck(string requestId, int amount, string itemName);

        [LoggerMessage(LogLevel.Warning,
            "Payment for request ID '{RequestId}' could not be processed. Requested {RequestedAmount} and only have {AvailableAmount} available of {ItemName}")]
        private partial void LogInsufficientInventory(string requestId, int requestedAmount, int availableAmount,
            string itemName);

        [LoggerMessage(LogLevel.Information, "There are not {Quantity} of {ItemName} left in stock")]
        private partial void LogUpdatedInventory(int quantity, string itemName);
    }

    public sealed partial class ProcessPaymentActivity(ILogger<ProcessPaymentActivity> logger)
        : WorkflowActivity<PaymentRequest, object?>
    {
        public override async Task<object?> RunAsync(WorkflowActivityContext context, PaymentRequest input)
        {
            LogProcessing(input.RequestId, input.Amount, input.ItemName, input.Currency);

            // Simulate slow processing
            await Task.Delay(TimeSpan.FromSeconds(7));

            LogProcessingSuccessful(input.RequestId);
            return null;
        }

        [LoggerMessage(LogLevel.Information,
            "Processing payment for order {RequestId} for {Amount} of {ItemName} at ${Currency}")]
        private partial void LogProcessing(string requestId, double amount, string itemName, double currency);

        [LoggerMessage(LogLevel.Information, "Payment for request ID '{RequestId}' processed successfully")]
        private partial void LogProcessingSuccessful(string requestId);
    }

    public sealed partial class ReserveInventoryActivity(
        ILogger<ReserveInventoryActivity> logger,
        DaprClient daprClient)
        : WorkflowActivity<InventoryRequest, InventoryResult>
    {
        public override async Task<InventoryResult> RunAsync(WorkflowActivityContext context, InventoryRequest req)
        {
            LogReservation(req.RequestId, req.Quantity, req.ItemName);

            // Ensure that the store has items
            var item = await daprClient.GetStateAsync<InventoryItem?>(
                TestContainers.Constants.DaprComponentNames.StateManagementComponentName,
                req.ItemName.ToLowerInvariant());

            // Catch for the case where the statestore isn't set up
            if (item == null)
            {
                // Not enough items
                return new InventoryResult(false, item);
            }

            LogAvailability(item.Quantity, item.Name);

            // See if there are enough items to purchase
            if (item.Quantity >= req.Quantity)
            {
                // Simulate slow processing
                await Task.Delay(TimeSpan.FromSeconds(2));
                return new InventoryResult(true, item);
            }

            // Not enough items
            return new InventoryResult(false, item);
        }

        [LoggerMessage(LogLevel.Information, "Reserving inventory for order '{RequestId}' of {Quantity} {ItemName}")]
        private partial void LogReservation(string requestId, int quantity, string itemName);

        [LoggerMessage(LogLevel.Information, "There are {Quantity} {ItemName} available for purchase")]
        private partial void LogAvailability(int Quantity, string ItemName);
    }

    public sealed partial class RequestApprovalActivity(ILogger<RequestApprovalActivity> logger)
        : WorkflowActivity<OrderPayload, object?>
    {
        public override Task<object?> RunAsync(WorkflowActivityContext context, OrderPayload input)
        {
            var orderId = context.InstanceId;
            LogApprovalRequest(orderId);
            return Task.FromResult<object?>(null);
        }

        [LoggerMessage(LogLevel.Information, "Requesting approval for order {Orderid}")]
        private partial void LogApprovalRequest(string orderId);
    }

    public sealed partial class NotifyActivity(ILogger<NotifyActivity> logger) : WorkflowActivity<Notification, object?>
    {
        public override Task<object?> RunAsync(WorkflowActivityContext context, Notification input)
        {
            LogNotification(input.Message);
            return Task.FromResult<object?>(null);
        }

        [LoggerMessage(LogLevel.Information, "A notification message was surfaced: '{Message}'")]
        private partial void LogNotification(string Message);
    }
}
