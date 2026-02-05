using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.Versioning.Patches;

public sealed class DuplicatePatchNameTest
{
    [Fact]
    public async Task Workflow_PatchVersioning_CompleteWithDuplicatePathName_WithActivities()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory(nameof(Workflow_PatchVersioning_CompleteWithDuplicatePathName_WithActivities));
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
                    opt =>
                    {
                        opt.RegisterWorkflow<SimpleWorkflow>();
                        opt.RegisterActivity<SimpleActivity1>();
                        opt.RegisterActivity<SimpleActivity2>();
                    },
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
        
        const int startingValue = 100;
    
        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(SimpleWorkflow), workflowInstanceId, startingValue);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        
        const int expectedValue = startingValue + (startingValue + 1) + (startingValue + 5) + (startingValue + 1);
        Assert.Equal(expectedValue, result.ReadOutputAs<int>());
    }
    
    private sealed class SimpleWorkflow : Workflow<int, int>
    {
        private const string Activity1PatchName = "activity1";
        private const string Activity2PatchName = "activity2";
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var result = input;
            if (context.IsPatched(Activity1PatchName))
            {
                result += await context.CallActivityAsync<int>(nameof(SimpleActivity1), input);
            }

            if (context.IsPatched(Activity2PatchName))
            {
                result += await context.CallActivityAsync<int>(nameof(SimpleActivity2), input);
            }

            if (context.IsPatched(Activity1PatchName))
            {
                result += await context.CallActivityAsync<int>(nameof(SimpleActivity1), input);
            }

            return result;
        }
    }

    private sealed class SimpleActivity1 : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input + 1);
        }
    }

    private sealed class SimpleActivity2 : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input + 5);
        }
    }
}
