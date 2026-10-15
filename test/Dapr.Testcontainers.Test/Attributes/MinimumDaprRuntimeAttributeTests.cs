using Dapr.Testcontainers.Xunit.Attributes;

namespace Dapr.Testcontainers.Test.Attributes;

[CollectionDefinition("Dapr runtime version attributes", DisableParallelization = true)]
public sealed class DaprRuntimeVersionAttributeCollection;

[Collection("Dapr runtime version attributes")]
public sealed class MinimumDaprRuntimeAttributeTests
{
    [Theory]
    [InlineData("1.15.9", "1.16.0", true)]
    [InlineData("1.16.0", "1.16.0", false)]
    [InlineData("1.17.0", "1.16.0", false)]
    [InlineData("2.0.0", "1.16.0", false)]
    [InlineData("1.16.0-rc.1", "1.16.0", false)]
    [InlineData("1.15.0-rc.1", "1.16.0", true)]
    [InlineData(" v1.16.0+build.1 ", "1.16.0", false)]
    [InlineData("1.16", "v1.16.0-rc.1+build.1", false)]
    [InlineData(null, "1.16.0", false)]
    [InlineData("", "1.16.0", false)]
    [InlineData(" ", "1.16.0", false)]
    [InlineData("latest", "1.16.0", false)]
    [InlineData("LATEST", "1.16.0", false)]
    [InlineData("invalid", "1.16.0", false)]
    public void AttributesUseTheSameVersionGate(string? currentVersion, string minimumVersion, bool shouldSkip)
    {
        var originalVersion = Environment.GetEnvironmentVariable("DAPR_RUNTIME_VERSION");
        try
        {
            Environment.SetEnvironmentVariable("DAPR_RUNTIME_VERSION", currentVersion);

            var fact = new MinimumDaprRuntimeFactAttribute(minimumVersion);
            var theory = new MinimumDaprRuntimeTheoryAttribute(minimumVersion);
            var expectedReason = shouldSkip
                ? $"Requires Dapr runtime >= {minimumVersion} (current: {currentVersion})."
                : null;

            Assert.Equal(expectedReason, fact.Skip);
            Assert.Equal(expectedReason, theory.Skip);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAPR_RUNTIME_VERSION", originalVersion);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("1")]
    public void AttributesRejectInvalidMinimumVersions(string? minimumVersion)
    {
        Assert.Throws<ArgumentException>("minimumVersion",
            () => new MinimumDaprRuntimeFactAttribute(minimumVersion!));
        Assert.Throws<ArgumentException>("minimumVersion",
            () => new MinimumDaprRuntimeTheoryAttribute(minimumVersion!));
    }

    [MinimumDaprRuntimeTheory("0.0")]
    [InlineData(1)]
    [InlineData(2)]
    public void TheoryDiscoversAndRunsDataRows(int value)
    {
        Assert.InRange(value, 1, 2);
    }
}
