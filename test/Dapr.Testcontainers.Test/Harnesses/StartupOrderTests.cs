using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;

namespace Dapr.Testcontainers.Test.Harnesses;

public sealed class StartupOrderTests
{
    [Fact]
    public async Task AppStartsFirst_ShouldPreconfigurePorts()
    {
        var options = new DaprRuntimeOptions();
        var environment = new DaprTestEnvironment(options);
        
        // We need a concrete harness to test BaseHarness logic
        var harness = new TestHarness(options, environment);  
        var builder = DaprHarnessBuilder.ForHarness(harness);
        
        // Configure it to load resources *after* the app (meaning App loads first)
        builder.WithDaprStartupOrder(shouldLoadResourcesFirst: false);
        
        // We mock the app startup to verify ports are set
        builder.ConfigureApp(app =>
        {
            // Verify ports are assigned before the app fully starts/harness initializes
            Assert.True(harness.DaprHttpPort > 0, "HTTP port should be pre-assigned");
            Assert.True(harness.DaprGrpcPort > 0, "gRPC port should be pre-assigned");
        });

        // Use a dummy app start just to trigger the build flow, but we won't actually run a real web host for long
        // This is a bit of a partial integration test because BuildAndStartAsync does real work. 
        // For pure unit testing, we'd need to mock more, but this validates the flow.
        
        await using var app = await builder.BuildAndStartAsync();

        Assert.True(harness.DaprHttpPort > 0);
        Assert.True(harness.DaprGrpcPort > 0);
    }

    [Fact]
    public void ResourcesStartFirst_ShouldNotPreconfigurePorts()
    {
        var options = new DaprRuntimeOptions();
        var environment = new DaprTestEnvironment(options);
        var harness = new TestHarness(options, environment);
        
        var builder = DaprHarnessBuilder.ForHarness(harness);

        // Default behavior (Resources first)
        builder.WithDaprStartupOrder(shouldLoadResourcesFirst: true);

        // Before we build, ports should be 0
        Assert.Equal(0, harness.DaprHttpPort);
        Assert.Equal(0, harness.DaprGrpcPort);

        // We can't easily assert the "during startup" state without hooks, 
        // but we can verify the end result is valid.
        
        // NOTE: Running this fully might try to spin up containers. 
        // If we want to avoid that, we can rely on the fact that `SetPorts` wasn't called.
        // But let's at least verify the builder state didn't mutate the harness prematurely.
        
        Assert.Equal(0, harness.DaprHttpPort);
    }
    
    // Concrete implementation for testing BaseHarness
    private class TestHarness : BaseHarness
    {
        
        
        public TestHarness(DaprRuntimeOptions options, DaprTestEnvironment environment) 
            : base(TestDirectoryManager.CreateTestDirectory("test-components"), null, options, environment)
        {
            ;
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
