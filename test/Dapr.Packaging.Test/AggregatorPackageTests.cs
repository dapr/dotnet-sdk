using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace Dapr.Packaging.Test;

public sealed class AggregatorPackageTests
{
    private static readonly string[] TargetFrameworks = ["net8.0", "net9.0", "net10.0"];

    public static TheoryData<AggregatorPackageCase> AggregatorPackages => new()
    {
        new AggregatorPackageCase(
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
            ]),
    };

    [Theory]
    [MemberData(nameof(AggregatorPackages))]
    public async Task AggregatorPackage_ExposesConsumerDependenciesAndBundledAssets(AggregatorPackageCase package)
    {
        var repoRoot = FindRepoRoot();
        var packagePath = await PackPackageAsync(repoRoot, package);

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nuspec = ReadNuspec(archive, package.PackageId);

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
            Directory.Delete(Path.GetDirectoryName(packagePath)!, recursive: true);
        }
    }

    private static async Task<string> PackPackageAsync(string repoRoot, AggregatorPackageCase package)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dapr-packaging-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        var projectPath = Path.Combine(repoRoot, package.ProjectPath);
        var result = await RunDotnetAsync(
            repoRoot,
            "pack",
            projectPath,
            "--configuration",
            "Debug",
            "--output",
            outputDirectory);

        Assert.True(
            result.ExitCode == 0,
            $"dotnet pack failed for {package.PackageId}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        var packages = Directory.GetFiles(outputDirectory, $"{package.PackageId}.*.nupkg");
        return Assert.Single(packages);
    }

    private static async Task<ProcessResult> RunDotnetAsync(string workingDirectory, params string[] arguments)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
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

    public sealed record AggregatorPackageCase(
        string PackageId,
        string ProjectPath,
        string[] RequiredDependencies,
        string[] RequiredLibAssets,
        string[] ForbiddenLibAssets)
    {
        public override string ToString() => PackageId;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
