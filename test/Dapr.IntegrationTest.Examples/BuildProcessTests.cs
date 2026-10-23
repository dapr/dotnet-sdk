// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Dapr.IntegrationTest.Examples;

public class BuildProcessTests
{
    [Fact]
    public async Task CapturesBothStreamsAndExitCode()
    {
        var startInfo = CreateShellCommand(
            "echo build-output & echo build-error 1>&2 & exit /b 7",
            "echo build-output; echo build-error >&2; exit 7");

        var (exitCode, output) = await ExampleBuildValidationTests.RunProcessAsync(startInfo, TimeSpan.FromSeconds(10));

        Assert.Equal(7, exitCode);
        Assert.Contains("build-output", output);
        Assert.Contains("build-error", output);
    }

    [Fact]
    public async Task TimeoutIncludesPartialOutput()
    {
        var startInfo = CreateShellCommand(
            "echo build-started & powershell -NoProfile -Command Start-Sleep -Seconds 30",
            "echo build-started; sleep 30");

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            ExampleBuildValidationTests.RunProcessAsync(startInfo, TimeSpan.FromSeconds(2))
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Contains("exceeded 2 second(s) limit", exception.Message);
        Assert.Contains("build-started", exception.Message);
    }

    [Fact]
    public async Task TimeoutAppliesToPipesHeldOpenAfterProcessExit()
    {
        // Unix descendants inherit redirected pipes, as MSBuild workers do on CI.
        if (OperatingSystem.IsWindows())
            Assert.Skip("This regression reproduces Unix pipe inheritance.");

        var startInfo = CreateShellCommand("", "sleep 5 & echo parent-exited; exit 0");

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            ExampleBuildValidationTests.RunProcessAsync(startInfo, TimeSpan.FromSeconds(1))
                .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));

        Assert.Contains("exceeded 1 second(s) limit", exception.Message);
        Assert.Contains("parent-exited", exception.Message);
    }

    private static ProcessStartInfo CreateShellCommand(string windowsCommand, string unixCommand)
    {
        var startInfo = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? windowsCommand : unixCommand);
        return startInfo;
    }
}
