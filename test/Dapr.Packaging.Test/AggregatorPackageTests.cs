using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace Dapr.Packaging.Test;

public sealed class AggregatorPackageTests
{
    private static readonly string[] TargetFrameworks = ["net8.0", "net9.0", "net10.0"];

    public static TheoryData<PackageWithBundledAssetsCase> PackagesWithBundledAssets => new()
    {
        new PackageWithBundledAssetsCase(
            PackageId: "Dapr.SecretsManagement",
            ProjectPath: Path.Combine("src", "Dapr.SecretsManagement", "Dapr.SecretsManagement.csproj"),
            RequiredDependencies:
            [
                "Dapr.Common",
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredLibAssets:
            [
                "Dapr.SecretsManagement.Abstractions.dll",
                "Dapr.SecretsManagement.Runtime.dll",
            ],
            ForbiddenLibAssets:
            [
                "Dapr.SecretsManagement.dll",
            ],
            RequiredAnalyzerAssets:
            [
                "Dapr.SecretsManagement.Generators.dll",
            ]),

        new PackageWithBundledAssetsCase(
            PackageId: "Dapr.StateManagement",
            ProjectPath: Path.Combine("src", "Dapr.StateManagement", "Dapr.StateManagement.csproj"),
            RequiredDependencies:
            [
                "Dapr.Common",
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredLibAssets:
            [
                "Dapr.StateManagement.Abstractions.dll",
                "Dapr.StateManagement.Runtime.dll",
            ],
            ForbiddenLibAssets:
            [
                "Dapr.StateManagement.dll",
            ],
            RequiredAnalyzerAssets:
            [
                "Dapr.StateManagement.Generators.dll",
            ]),

        new PackageWithBundledAssetsCase(
            PackageId: "Dapr.Metadata",
            ProjectPath: Path.Combine("src", "Dapr.Metadata", "Dapr.Metadata.csproj"),
            RequiredDependencies:
            [
                "Dapr.Common",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Hosting.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredLibAssets:
            [
                "Dapr.Metadata.Abstractions.dll",
                "Dapr.Metadata.Runtime.dll",
            ],
            ForbiddenLibAssets:
            [
                "Dapr.Metadata.dll",
            ],
            RequiredAnalyzerAssets:
            [
            ]),

        new PackageWithBundledAssetsCase(
            PackageId: "Dapr.Workflow",
            ProjectPath: Path.Combine("src", "Dapr.Workflow", "Dapr.Workflow.csproj"),
            RequiredDependencies:
            [
                "Dapr.Common",
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Grpc.Net.ClientFactory",
                "Microsoft.Extensions.Hosting",
                "Microsoft.Extensions.Http",
            ],
            RequiredLibAssets:
            [
                "Dapr.Workflow.dll",
                "Dapr.Workflow.Abstractions.dll",
                "Dapr.Workflow.Grpc.dll",
                "Dapr.Workflow.Versioning.Abstractions.dll",
                "Dapr.Workflow.Versioning.Runtime.dll",
            ],
            ForbiddenLibAssets:
            [
            ],
            RequiredAnalyzerAssets:
            [
                "Dapr.Workflow.Analyzers.dll",
                "Dapr.Workflow.Versioning.Generators.dll",
            ]),

    };

    [Theory]
    [MemberData(nameof(PackagesWithBundledAssets))]
    public async Task Package_ExposesConsumerDependenciesAndExpectedAssets(PackageWithBundledAssetsCase package)
    {
        var repoRoot = FindRepoRoot();
        var packageOutput = await PackPackageAsync(repoRoot, package.PackageId, package.ProjectPath);

        try
        {
            using var archive = ZipFile.OpenRead(packageOutput.PackagePath);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nuspec = ReadNuspec(archive, package.PackageId);

            foreach (var requiredAnalyzerAsset in package.RequiredAnalyzerAssets)
            {
                Assert.Contains($"analyzers/dotnet/cs/{requiredAnalyzerAsset}", entryNames);
            }

            foreach (var targetFramework in TargetFrameworks)
            {
                var dependencies = GetDependencyIds(nuspec, targetFramework);

                foreach (var requiredDependency in package.RequiredDependencies)
                {
                    Assert.Contains(requiredDependency, dependencies);
                }

                foreach (var requiredLibAsset in package.RequiredLibAssets)
                {
                    Assert.Contains($"lib/{targetFramework}/{requiredLibAsset}", entryNames);
                }

                foreach (var forbiddenLibAsset in package.ForbiddenLibAssets)
                {
                    Assert.DoesNotContain($"lib/{targetFramework}/{forbiddenLibAsset}", entryNames);
                }
            }
        }
        finally
        {
            packageOutput.Dispose();
        }
    }

    private static async Task<PackageOutput> PackPackageAsync(string repoRoot, string packageId, string projectPath)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dapr-packaging-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        return await PackPackageToDirectoryAsync(repoRoot, packageId, projectPath, outputDirectory, ownsDirectory: true);
    }

    private static async Task<PackageOutput> PackPackageToDirectoryAsync(
        string repoRoot,
        string packageId,
        string projectPath,
        string outputDirectory,
        bool ownsDirectory = false)
    {
        var fullProjectPath = Path.Combine(repoRoot, projectPath);
        var result = await RunDotnetAsync(
            repoRoot,
            "pack",
            fullProjectPath,
            "--configuration",
            "Debug",
            "--no-restore",
            "--output",
            outputDirectory);

        Assert.True(
            result.ExitCode == 0,
            $"dotnet pack failed for {packageId}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        var packages = Directory
            .GetFiles(outputDirectory, $"{packageId}.*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        return new PackageOutput(Assert.Single(packages), ownsDirectory ? outputDirectory : null);
    }

    private static async Task<ProcessResult> RunDotnetAsync(string workingDirectory, params string[] arguments)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {string.Join(" ", arguments)} timed out.");
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static XDocument ReadNuspec(ZipArchive archive, string packageId)
    {
        var nuspecEntry = archive.GetEntry($"{packageId}.nuspec");
        Assert.NotNull(nuspecEntry);

        using var stream = nuspecEntry.Open();
        return XDocument.Load(stream);
    }

    private static HashSet<string> GetDependencyIds(XDocument nuspec, string targetFramework)
    {
        XNamespace ns = nuspec.Root?.Name.Namespace ?? XNamespace.None;
        var group = nuspec
            .Descendants(ns + "group")
            .SingleOrDefault(element => string.Equals(
                (string?)element.Attribute("targetFramework"),
                targetFramework,
                StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(group);

        return group
            .Elements(ns + "dependency")
            .Select(element => (string?)element.Attribute("id"))
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "test")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    public sealed record PackageWithBundledAssetsCase(
        string PackageId,
        string ProjectPath,
        string[] RequiredDependencies,
        string[] RequiredLibAssets,
        string[] ForbiddenLibAssets,
        string[] RequiredAnalyzerAssets)
    {
        public override string ToString() => PackageId;
    }

    private sealed record PackageOutput(string PackagePath, string? OwnedDirectory) : IDisposable
    {
        public void Dispose()
        {
            if (OwnedDirectory is not null && Directory.Exists(OwnedDirectory))
            {
                Directory.Delete(OwnedDirectory, recursive: true);
            }
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
