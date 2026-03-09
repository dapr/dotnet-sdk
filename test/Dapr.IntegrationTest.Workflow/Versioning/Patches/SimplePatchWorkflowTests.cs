using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning.Patches;

public sealed class SimplePatchWorkflowTests
{
    [MinimumDaprRuntimeFact("1.17.0")]
    public async Task Workflow_PatchVersioning_CompleteWithPatchTaken_NoActivities()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("patch-versioning");
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
                    opt => opt.RegisterWorkflow<SimpleWorkflow>(),
                    configureClient: (sp, cb) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            cb.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();
        
        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        
        const int startingValue = 0;
    
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(SimpleWorkflow), workflowInstanceId, startingValue);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        
        Assert.Equal(startingValue + 10, result.ReadOutputAs<int>());
    }
    
    private sealed class SimpleWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input)
        {
            var result = context.IsPatched("v1") ? input + 10 : input + 100;
            return Task.FromResult(result);
        }
    }
}

