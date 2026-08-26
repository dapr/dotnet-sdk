#nowarn "FS3261"
namespace WorkflowConsoleApp.Activities

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Dapr.Client
open Dapr.Workflow
open WorkflowConsoleApp

type NotifyActivity(loggerFactory: ILoggerFactory) =
    inherit WorkflowActivity<Notification, obj>()
    let logger = loggerFactory.CreateLogger<NotifyActivity>()

    override _.RunAsync(_: WorkflowActivityContext, notification: Notification) : Task<obj> =
        logger.LogInformation(notification.Message)
        Task.FromResult<obj>(Unchecked.defaultof<obj>)


type ReserveInventoryActivity(loggerFactory: ILoggerFactory, client: DaprClient) =
    inherit WorkflowActivity<InventoryRequest, InventoryResult>()
    let logger = loggerFactory.CreateLogger<ReserveInventoryActivity>()
    static let storeName = "statestore"

    override _.RunAsync(_: WorkflowActivityContext, req: InventoryRequest) : Task<InventoryResult> =
        task {
            logger.LogInformation(
                "Reserving inventory for order '{requestId}' of {quantity} {name}",
                req.RequestId, req.Quantity, req.ItemName)

            let! item = client.GetStateAsync<InventoryItem>(storeName, req.ItemName.ToLowerInvariant())

            if isNull (box item) then
                return { Success = false; OrderPayload = item }
            else
                logger.LogInformation(
                    "There are {quantity} {name} available for purchase",
                    item.Quantity, item.Name)

                if item.Quantity >= req.Quantity then
                    do! Task.Delay(TimeSpan.FromSeconds(2.0))
                    return { Success = true; OrderPayload = item }
                else
                    return { Success = false; OrderPayload = item }
        }


type RequestApprovalActivity(loggerFactory: ILoggerFactory) =
    inherit WorkflowActivity<OrderPayload, obj>()
    let logger = loggerFactory.CreateLogger<RequestApprovalActivity>()

    override _.RunAsync(context: WorkflowActivityContext, input: OrderPayload) : Task<obj> =
        let orderId = context.InstanceId.ToString()
        logger.LogInformation("Requesting approval for order {orderId}", orderId)
        Task.FromResult<obj>(Unchecked.defaultof<obj>)


type ProcessPaymentActivity(loggerFactory: ILoggerFactory) =
    inherit WorkflowActivity<PaymentRequest, obj>()
    let logger = loggerFactory.CreateLogger<ProcessPaymentActivity>()

    override _.RunAsync(_: WorkflowActivityContext, req: PaymentRequest) : Task<obj> =
        task {
            logger.LogInformation(
                "Processing payment: {requestId} for {amount} {item} at ${currency}",
                req.RequestId, req.Amount, req.ItemName, req.Currency)

            do! Task.Delay(TimeSpan.FromSeconds(7.0))

            logger.LogInformation(
                "Payment for request ID '{requestId}' processed successfully",
                req.RequestId)

            return Unchecked.defaultof<obj>
        }


type UpdateInventoryActivity(loggerFactory: ILoggerFactory, client: DaprClient) =
    inherit WorkflowActivity<PaymentRequest, obj>()
    let logger = loggerFactory.CreateLogger<UpdateInventoryActivity>()
    static let storeName = "statestore"

    override _.RunAsync(_: WorkflowActivityContext, req: PaymentRequest) : Task<obj> =
        task {
            logger.LogInformation(
                "Checking inventory for order '{requestId}' for {amount} {name}",
                req.RequestId, req.Amount, req.ItemName)

            do! Task.Delay(TimeSpan.FromSeconds(5.0))

            let! item = client.GetStateAsync<InventoryItem>(storeName, req.ItemName.ToLowerInvariant())
            let newQuantity = item.Quantity - req.Amount

            if newQuantity < 0 then
                logger.LogInformation(
                    "Payment for request ID '{requestId}' could not be processed. Insufficient inventory.",
                    req.RequestId)
                raise (InvalidOperationException(
                    $"Not enough '{req.ItemName}' inventory! Requested {req.Amount} but only {item.Quantity} available."))

            do! client.SaveStateAsync(
                storeName,
                req.ItemName.ToLowerInvariant(),
                { Name = req.ItemName; PerItemCost = item.PerItemCost; Quantity = newQuantity })

            logger.LogInformation(
                "There are now {quantity} {name} left in stock",
                newQuantity, item.Name)

            return Unchecked.defaultof<obj>
        }