using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class PlainWorkflowTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldHandlePlainWorkflow(bool loadResourcesFirst)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<TestWorkflow>();
                    },
                    configureClient: (sp, cb) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            cb.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .WithDaprStartupOrder(loadResourcesFirst)
            .BuildAndStartAsync();
        
        // Clean test logic
        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        
        // Start the workflow
        const int startingValue = 0;

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TestWorkflow), workflowInstanceId, startingValue);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId, true);
        
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var resultValue = result.ReadOutputAs<int>();
        Assert.Equal(startingValue + 100, resultValue);
    }
    
    private sealed class TestWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input) => Task.FromResult(input + 100);
    }
}
