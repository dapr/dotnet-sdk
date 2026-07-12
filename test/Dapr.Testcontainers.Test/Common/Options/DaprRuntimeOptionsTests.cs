using Dapr.Testcontainers.Common.Options;

using Dapr.Testcontainers.Common;

namespace Dapr.Testcontainers.Test.Common.Options;

public sealed class DaprRuntimeOptionsTests : IDisposable
{
    private const string DaprRuntimeVersionEnvVarName = "DAPR_RUNTIME_VERSION";
    
    [Fact]
    public void ShouldUseEnvVarVersion_IfSet()
    {
        const string version = "1.16.2";
        Environment.SetEnvironmentVariable(DaprRuntimeVersionEnvVarName, version);

        var options = new DaprRuntimeOptions();
        Assert.Equal(version, options.Version);
    }

    [Fact]
    public void ShouldUseDefaultVersionIfEnvVarNotTest()
    {
        const string defaultValue = "latest";

        var options = new DaprRuntimeOptions();
        Assert.Equal(defaultValue, options.Version);
    }

    [Fact]
    public void ShouldEnableContainerLogsForCiDebugLogging()
    {
        var originalCi = Environment.GetEnvironmentVariable("CI");

        try
        {
            Environment.SetEnvironmentVariable("CI", "true");

            var options = new DaprRuntimeOptions()
                .WithLogLevel(DaprLogLevel.Debug);

            Assert.True(options.EnableContainerLogs);
            Assert.False(string.IsNullOrWhiteSpace(options.ContainerLogsDirectory));

            TestDirectoryManager.CleanUpDirectory(options.ContainerLogsDirectory!);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", originalCi);
        }
    }

    [Fact]
    public void ShouldSetEnvironmentVariable()
    {
        var options = new DaprRuntimeOptions()
            .WithEnvironmentVariable("REDIS_HOST", "redis:6379")
            .WithEnvironmentVariable("REDIS_PASSWORD", "password");

        Assert.Equal("redis:6379", options.EnvironmentVariables["REDIS_HOST"]);
        Assert.Equal("password", options.EnvironmentVariables["REDIS_PASSWORD"]);
    }

    [Fact]
    public void ShouldOverwriteEnvironmentVariable()
    {
        var options = new DaprRuntimeOptions()
            .WithEnvironmentVariable("REDIS_HOST", "redis:6379")
            .WithEnvironmentVariable("REDIS_HOST", "redis:6380");

        Assert.Equal("redis:6380", options.EnvironmentVariables["REDIS_HOST"]);
    }

    public void Dispose()
    {
        // Clear this variable
        Environment.SetEnvironmentVariable(DaprRuntimeVersionEnvVarName, null);
    }
}
