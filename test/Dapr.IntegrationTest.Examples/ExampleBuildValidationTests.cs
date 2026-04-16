// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Dapr.IntegrationTest.Examples;

/// <summary>
/// Validates that all example projects in the repository build successfully.
/// This ensures SDK changes do not break existing examples across versions.
/// </summary>
public class ExampleBuildValidationTests
{
    /// <summary>
    /// Timeout for each individual project build.
    /// </summary>
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Paths (relative to the examples directory) to exclude from build validation.
    /// These projects depend on external NuGet packages (e.g. Aspire SDK) that may not
    /// be available in all CI environments and don't validate source-level compatibility.
    /// </summary>
    private static readonly string[] ExcludedPathPrefixes =
    [
        $"Hosting{Path.DirectorySeparatorChar}"
    ];

    /// <summary>
    /// Discovers all example project files suitable for build validation.
    /// </summary>
    public static TheoryData<string> GetExampleProjects()
    {
        var data = new TheoryData<string>();
        var repoRoot = FindRepoRoot();
        var examplesDir = Path.Combine(repoRoot, "examples");

        foreach (var csproj in Directory.GetFiles(examplesDir, "*.csproj", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(examplesDir, csproj);

            if (ExcludedPathPrefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                continue;

            data.Add(relativePath);
        }

        return data;
    }

    /// <summary>
    /// Verifies that each example project builds successfully against the current SDK source.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetExampleProjects))]
    public async Task ExampleProject_ShouldBuildSuccessfully(string relativeProjectPath)
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, "examples", relativeProjectPath);

        Assert.True(File.Exists(fullPath), $"Project file not found: {fullPath}");

        var (exitCode, output) = await RunDotnetBuildAsync(fullPath);

        Assert.True(exitCode == 0,
            $"Build failed for '{relativeProjectPath}' (exit code {exitCode}):{Environment.NewLine}{output}");
    }

    /// <summary>
    /// Verifies that the WorkflowUnitTest example's tests pass when executed.
    /// </summary>
    [Fact]
    public async Task WorkflowUnitTestExample_ShouldPassTests()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "examples", "Workflow", "WorkflowUnitTest", "WorkflowUnitTest.csproj");

        Assert.True(File.Exists(projectPath), $"WorkflowUnitTest project not found: {projectPath}");

        var (exitCode, output) = await RunDotnetTestAsync(projectPath);

        Assert.True(exitCode == 0,
            $"Tests failed for WorkflowUnitTest (exit code {exitCode}):{Environment.NewLine}{output}");
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetBuildAsync(string projectPath)
    {
        return await RunDotnetCommandAsync($"build \"{projectPath}\" --framework net10.0 --no-incremental -consoleLoggerParameters:NoSummary");
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetTestAsync(string projectPath)
    {
        return await RunDotnetCommandAsync($"test \"{projectPath}\" --framework net10.0");
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetCommandAsync(string arguments)
    {
        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process();
        process.StartInfo = psi;

        var outputBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                outputBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(BuildTimeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            outputBuilder.AppendLine($"[TIMEOUT] Build exceeded {BuildTimeout.TotalMinutes} minute(s) limit.");
        }

        return (process.ExitCode, outputBuilder.ToString());
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "all.sln")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find repository root. Ensure the test is run from within the repository directory tree.");
    }
}
