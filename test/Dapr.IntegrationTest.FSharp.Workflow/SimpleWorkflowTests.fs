#nowarn "FS3261"
namespace Dapr.IntegrationTest.FSharp.Workflow

open System
open System.Threading.Tasks
open Dapr.Testcontainers.Common
open Dapr.Testcontainers.Harnesses
open Dapr.Workflow
open Dapr.Workflow.Registration
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Xunit

type SimpleWorkflowTests() =

    [<Fact>]
    member this.ShouldHandleSimpleWorkflow() = task {
        let componentsDir = TestDirectoryManager.CreateTestDirectory("fsharp-workflow-components")
        let workflowInstanceId = Guid.NewGuid().ToString()

        use! env = DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState = true,
            cancellationToken = TestContext.Current.CancellationToken)
        do! env.StartAsync(TestContext.Current.CancellationToken)

        let harness =
            DaprHarnessBuilder(componentsDir)
                .WithEnvironment(env)
                .BuildWorkflow()

        let! testApp =
            DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(fun builder ->
                    builder.Services.AddDaprWorkflowBuilder(
                        Action<WorkflowRuntimeOptions>(fun opt ->
                            opt.RegisterWorkflow<TestWorkflow>()
                            opt.RegisterActivity<DoublingActivity>()),
                        Action<IServiceProvider, DaprWorkflowClientBuilder>(fun sp clientBuilder ->
                            let config = sp.GetRequiredService<IConfiguration>()
                            let grpcEndpoint = config.["DAPR_GRPC_ENDPOINT"]
                            if not (String.IsNullOrEmpty(grpcEndpoint)) then
                                clientBuilder.UseGrpcEndpoint(grpcEndpoint) |> ignore)) |> ignore)
                .WithDaprStartupOrder(true)
                .BuildAndStartAsync()
        use testApp = testApp

        use scope = testApp.CreateScope()
        let daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>()

        let startingValue = 8
        let! _ = daprWorkflowClient.ScheduleNewWorkflowAsync(typeof<TestWorkflow>.Name, workflowInstanceId, startingValue)
        let! result = daprWorkflowClient.WaitForWorkflowCompletionAsync(
            workflowInstanceId, true, TestContext.Current.CancellationToken)

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus)
        let resultValue = result.ReadOutputAs<int>()
        Assert.Equal(16, resultValue)
    }

and DoublingActivity() =
    inherit WorkflowActivity<int, int>()
    override _.RunAsync(_: WorkflowActivityContext, input: int) : Task<int> =
        Task.FromResult<int>(input * 2)

and TestWorkflow() =
    inherit Workflow<int, int>()
    override _.RunAsync(context: WorkflowContext, input: int) : Task<int> =
        task {
            let! result = context.CallActivityAsync<int>(typeof<DoublingActivity>.Name, input)
            return result
        }
