using Dapr.TestContainers.Common;
using Dapr.TestContainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning.Patches;

public sealed class SimplePatchWorkflowTests
{
    [Fact]
    public async Task Workflow_PatchVersioning_CompleteWithPatchNotTaken_NoActivities()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("patch-versioning");
        var workflowInstanceId = Guid.NewGuid().ToString();
        const int startingValue = 10;
        
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
    
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(SimpleWorkflow), workflowInstanceId, "test");
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        
        Assert.Equal(startingValue + 100, result.ReadOutputAs<int>());
    }
    
    // [Fact]
    // public async Task Workflow_PatchVersioning_CompleteWithSuccessfulPatch_Standard()
    // {
    //     
    // }

    private sealed class SimpleWorkflow : Workflow<int, int>
    {
        public override Task<int> RunAsync(WorkflowContext context, int input)
        {
            int result;
            if (context.IsPatched("v1"))
            {
                result = input + 10;
            }
            else
            {
                result = input + 100;
            }

            return Task.FromResult(result);
        }
    }

    private sealed class SimpleWorkflow2 : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            if (context.IsPatched("v1"))
            {
                return await context.CallActivityAsync<int>(nameof(SimpleActivity), input);
            }

            return input;
        }
    }

    private sealed class SimpleActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input + 10);
        }
    }
}
