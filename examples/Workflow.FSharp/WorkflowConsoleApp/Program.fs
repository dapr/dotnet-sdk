#nowarn "FS3261"
open System
open System.Linq
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Dapr.Client
open Dapr.Workflow
open WorkflowConsoleApp
open WorkflowConsoleApp.Activities
open WorkflowConsoleApp.Workflows

let storeName = "statestore"

let restockInventory (daprClient: DaprClient) (inventory: InventoryItem seq) =
    task {
        Console.WriteLine("*** Restocking inventory...")
        for item in inventory do
            Console.WriteLine($"*** \t{item.Name}: {item.Quantity}")
            do! daprClient.SaveStateAsync(storeName, item.Name.ToLowerInvariant(), item)
    }

let readLineTrimmed () =
    let input = Console.ReadLine()
    if isNull input then "" else input.Trim()

let rec waitForCompletion (daprWorkflowClient: DaprWorkflowClient) (orderId: string) : Task<WorkflowState> =
    task {
        use cts = new CancellationTokenSource(TimeSpan.FromSeconds(5.0))
        try
            let! s = daprWorkflowClient.WaitForWorkflowCompletionAsync(
                        instanceId = orderId,
                        cancellation = cts.Token)
            return s
        with :? OperationCanceledException ->
            let! s = daprWorkflowClient.GetWorkflowStateAsync(instanceId = orderId)
            let status = s.ReadCustomStatusAs<string>()
            if not (isNull status) && status.Contains("Waiting for approval") then
                Console.WriteLine(
                    $"{typeof<OrderProcessingWorkflow>.Name} (ID = {orderId}) requires approval. Approve? [Y/N]")
                let approval = Console.ReadLine()
                let mutable approvalResult = ApprovalResult.Unspecified
                if String.Equals(approval, "Y", StringComparison.OrdinalIgnoreCase) then
                    Console.WriteLine("Approving order...")
                    approvalResult <- ApprovalResult.Approved
                elif String.Equals(approval, "N", StringComparison.OrdinalIgnoreCase) then
                    Console.WriteLine("Rejecting order...")
                    approvalResult <- ApprovalResult.Rejected

                if approvalResult <> ApprovalResult.Unspecified then
                    do! daprWorkflowClient.RaiseEventAsync(
                            instanceId = orderId,
                            eventName = "ManagerApproval",
                            eventPayload = approvalResult)
            return! waitForCompletion daprWorkflowClient orderId
    }

[<EntryPoint>]
let main (args: string[]) =
    let configureWorkflows (options: WorkflowRuntimeOptions) =
        options.RegisterWorkflow<OrderProcessingWorkflow>()
        options.RegisterActivity<NotifyActivity>()
        options.RegisterActivity<ReserveInventoryActivity>()
        options.RegisterActivity<RequestApprovalActivity>()
        options.RegisterActivity<ProcessPaymentActivity>()
        options.RegisterActivity<UpdateInventoryActivity>()

    let builder =
        Host.CreateDefaultBuilder(args).ConfigureServices(
            Action<IServiceCollection>(fun services ->
                services.AddDaprClient() |> ignore
                services.AddDaprWorkflow(Action<WorkflowRuntimeOptions>(configureWorkflows)) |> ignore))

    if String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAPR_GRPC_PORT")) then
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "50001")

    Console.ForegroundColor <- ConsoleColor.White
    Console.WriteLine("*** Welcome to the Dapr Workflow console app sample!")
    Console.WriteLine("*** Using this app, you can place orders that start workflows.")
    Console.WriteLine("*** Ensure that Dapr is running in a separate terminal window using the following command:")
    Console.ForegroundColor <- ConsoleColor.Green
    Console.WriteLine("        dapr run --dapr-grpc-port 50001 --app-id wfapp")
    Console.WriteLine()
    Console.ResetColor()

    use host = builder.Build()
    host.Start()

    let go =
        task {
            use daprClient =
                let apiToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN")
                if not (String.IsNullOrEmpty(apiToken)) then
                    DaprClientBuilder().UseDaprApiToken(apiToken).Build()
                else
                    DaprClientBuilder().Build()

            let mutable healthy = false
            while not healthy do
                healthy <- daprClient.CheckHealthAsync().GetAwaiter().GetResult()
                if not healthy then
                    Thread.Sleep(TimeSpan.FromSeconds(5.0))

            Thread.Sleep(TimeSpan.FromSeconds(1.0))

            let baseInventory = [
                { Name = "Paperclips"; PerItemCost = 5.0; Quantity = 100 }
                { Name = "Cars"; PerItemCost = 15000.0; Quantity = 100 }
                { Name = "Computers"; PerItemCost = 500.0; Quantity = 100 }
            ]

            do! restockInventory daprClient baseInventory

            let quit = ref false
            Console.CancelKeyPress.Add(fun _ ->
                quit.Value <- true
                Console.WriteLine("Shutting down the example."))

            while not quit.Value do
                let items = String.Join(", ", baseInventory.Select(fun i -> i.Name))
                Console.WriteLine($"Enter the name of one of the following items to order [{items}].")
                Console.WriteLine("To restock items, type 'restock'.")
                let itemName = readLineTrimmed ()

                if String.IsNullOrEmpty(itemName) then
                    ()
                elif String.Equals("restock", itemName, StringComparison.OrdinalIgnoreCase) then
                    do! restockInventory daprClient baseInventory
                else
                    let item = baseInventory.FirstOrDefault(fun i ->
                        String.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase))

                    if isNull (box item) then
                        Console.ForegroundColor <- ConsoleColor.Yellow
                        Console.WriteLine($"We don't have {itemName}!")
                        Console.ResetColor()
                    else
                        Console.WriteLine($"How many {itemName} would you like to purchase?")
                        let amountStr = readLineTrimmed ()
                        let mutable amount = 1
                        if not (Int32.TryParse(amountStr, &amount)) || amount <= 0 then
                            Console.ForegroundColor <- ConsoleColor.Yellow
                            Console.WriteLine("Invalid input. Assuming you meant to type '1'.")
                            Console.ResetColor()
                            amount <- 1

                        let daprWorkflowClient = host.Services.GetRequiredService<DaprWorkflowClient>()

                        let guidStr = Guid.NewGuid().ToString()
                        let orderId = $"{itemName.ToLowerInvariant()}-{guidStr.[..7]}"
                        let totalCost = float amount * item.PerItemCost
                        let orderInfo = { Name = itemName.ToLowerInvariant(); TotalCost = totalCost; Quantity = amount }

                        Console.WriteLine($"Starting order workflow '{orderId}' purchasing {amount} {itemName}")
                        let! _ = daprWorkflowClient.ScheduleNewWorkflowAsync(
                                     name = typeof<OrderProcessingWorkflow>.Name,
                                     input = orderInfo,
                                     instanceId = orderId)

                        let! state = daprWorkflowClient.WaitForWorkflowStartAsync(instanceId = orderId)
                        Console.WriteLine(
                            $"{typeof<OrderProcessingWorkflow>.Name} (ID = {orderId}) started successfully with {state.ReadInputAs<OrderPayload>()}")

                        let! state = waitForCompletion daprWorkflowClient orderId

                        if state.RuntimeStatus = WorkflowRuntimeStatus.Completed then
                            let result = state.ReadOutputAs<OrderResult>()
                            if not (isNull (box result)) && result.Processed then
                                Console.ForegroundColor <- ConsoleColor.Green
                                Console.WriteLine($"Order workflow is {state.RuntimeStatus} and the order was processed successfully ({result}).")
                                Console.ResetColor()
                            else
                                Console.WriteLine($"Order workflow is {state.RuntimeStatus} but the order was not processed.")
                        elif state.RuntimeStatus = WorkflowRuntimeStatus.Failed then
                            Console.ForegroundColor <- ConsoleColor.Red
                            Console.WriteLine($"The workflow failed - {state.FailureDetails}")
                            Console.ResetColor()

                        Console.WriteLine()
        }

    go.GetAwaiter().GetResult()
    0