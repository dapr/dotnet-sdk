using Dapr.Testcontainers.Common.Options;

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

    public void Dispose()
    {
        // Clear this variable
        Environment.SetEnvironmentVariable(DaprRuntimeVersionEnvVarName, null);
    }
}
