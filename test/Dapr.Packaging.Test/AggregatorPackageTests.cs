using System.Xml.Linq;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Packaging.Test;

#if NET10_0_OR_GREATER
public sealed class AggregatorPackageTests
{
    public static TheoryData<PackageContractCase> PackagesWithBundledAssets => new()
    {
        new PackageContractCase(
            PackageId: "Dapr.SecretsManagement",
            ProjectPath: Path.Combine("src", "Dapr.SecretsManagement", "Dapr.SecretsManagement.csproj"),
            RequiredProjectReferences:
            [
                "Dapr.Common.csproj",
            ],
            RequiredPackageReferences:
            [
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredBundledProjects:
            [
                "Dapr.SecretsManagement.Abstractions.csproj",
                "Dapr.SecretsManagement.Runtime.csproj",
            ],
            RequiredAnalyzerProjects:
            [
                "Dapr.SecretsManagement.Generators.csproj",
            ],
            IncludeBuildOutput: false),

        new PackageContractCase(
            PackageId: "Dapr.StateManagement",
            ProjectPath: Path.Combine("src", "Dapr.StateManagement", "Dapr.StateManagement.csproj"),
            RequiredProjectReferences:
            [
                "Dapr.Common.csproj",
            ],
            RequiredPackageReferences:
            [
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredBundledProjects:
            [
                "Dapr.StateManagement.Abstractions.csproj",
                "Dapr.StateManagement.Runtime.csproj",
            ],
            RequiredAnalyzerProjects:
            [
                "Dapr.StateManagement.Generators.csproj",
            ],
            IncludeBuildOutput: false),

        new PackageContractCase(
            PackageId: "Dapr.Metadata",
            ProjectPath: Path.Combine("src", "Dapr.Metadata", "Dapr.Metadata.csproj"),
            RequiredProjectReferences:
            [
                "Dapr.Common.csproj",
            ],
            RequiredPackageReferences:
            [
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Hosting.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredBundledProjects:
            [
                "Dapr.Metadata.Abstractions.csproj",
                "Dapr.Metadata.Runtime.csproj",
            ],
            RequiredAnalyzerProjects:
            [
            ],
            IncludeBuildOutput: false),

        new PackageContractCase(
            PackageId: "Dapr.Messaging",
            ProjectPath: Path.Combine("src", "Dapr.Messaging", "Dapr.Messaging.csproj"),
            RequiredProjectReferences:
            [
                "Dapr.Common.csproj",
            ],
            RequiredPackageReferences:
            [
                "Google.Protobuf",
                "Grpc.AspNetCore",
                "Grpc.Net.Client",
                "Microsoft.Extensions.DependencyInjection",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Hosting.Abstractions",
                "Microsoft.Extensions.Logging.Abstractions",
                "Microsoft.Extensions.Options",
            ],
            RequiredBundledProjects:
            [
                "Dapr.Messaging.Abstractions.csproj",
                "Dapr.Messaging.Runtime.csproj",
            ],
            RequiredAnalyzerProjects:
            [
                "Dapr.Messaging.Generators.csproj",
                "Dapr.Messaging.Analyzers.csproj",
            ],
            IncludeBuildOutput: false),

        new PackageContractCase(
            PackageId: "Dapr.Workflow",
            ProjectPath: Path.Combine("src", "Dapr.Workflow", "Dapr.Workflow.csproj"),
            RequiredProjectReferences:
            [
                "Dapr.Common.csproj",
            ],
            RequiredPackageReferences:
            [
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Grpc.Net.ClientFactory",
                "Microsoft.Extensions.Hosting",
                "Microsoft.Extensions.Http",
            ],
            RequiredBundledProjects:
            [
                "Dapr.Workflow.Abstractions.csproj",
                "Dapr.Workflow.Grpc.csproj",
                "Dapr.Workflow.Versioning.Abstractions.csproj",
                "Dapr.Workflow.Versioning.Runtime.csproj",
            ],
            RequiredAnalyzerProjects:
            [
                "Dapr.Workflow.Analyzers.csproj",
                "Dapr.Workflow.Versioning.Generators.csproj",
            ],
            IncludeBuildOutput: true),
    };

    [Theory]
    [MemberData(nameof(PackagesWithBundledAssets))]
    public void AggregatorPackage_ProjectFilePreservesPackagingContract(PackageContractCase package)
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, package.ProjectPath);
        var project = XDocument.Load(projectPath);
        var elements = project.Descendants().ToArray();

        Assert.Equal(package.PackageId, GetPropertyValue(elements, "PackageId"));
        Assert.DoesNotContain(elements, element => element.Name.LocalName == "SuppressDependenciesWhenPacking");
        Assert.Equal(package.IncludeBuildOutput, GetBooleanPropertyValue(elements, "IncludeBuildOutput", defaultValue: true));

        var projectReferences = GetItemIncludes(elements, "ProjectReference");
        var packageReferences = GetItemIncludes(elements, "PackageReference");
        var bundledProjectReferences = GetItemIncludes(elements, element => element.Name.LocalName.EndsWith("ChildLib", StringComparison.Ordinal));
        var projectPathMentions = GetProjectPathMentions(elements);

        foreach (var requiredProjectReference in package.RequiredProjectReferences)
        {
            Assert.Contains(projectReferences, reference => GetProjectFileName(reference) == requiredProjectReference);
        }

        foreach (var requiredPackageReference in package.RequiredPackageReferences)
        {
            Assert.Contains(requiredPackageReference, packageReferences);
        }

        foreach (var requiredBundledProject in package.RequiredBundledProjects)
        {
            Assert.Contains(bundledProjectReferences, reference => GetProjectFileName(reference) == requiredBundledProject);
        }

        foreach (var requiredAnalyzerProject in package.RequiredAnalyzerProjects)
        {
            Assert.Contains(projectPathMentions, reference => GetProjectFileName(reference) == requiredAnalyzerProject);
        }

        AssertTargetsTfmSpecificPackageFiles(elements);
    }

    [Fact]
    public void DaprMessagingPackage_ContainsRuntimeAnalyzersCodeFixAndSourceGenerator()
    {
        var repoRoot = FindRepoRoot();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dapr-messaging-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var result = PackDaprMessaging(repoRoot, outputDirectory);
            Assert.True(
                result.ExitCode == 0,
                $"dotnet pack failed with exit code {result.ExitCode}.{Environment.NewLine}{result.Output}");

            var packagePath = Assert.Single(Directory.EnumerateFiles(outputDirectory, "Dapr.Messaging.*.nupkg"));
            using var package = ZipFile.OpenRead(packagePath);
            var entries = package.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var framework in new[] { "net8.0", "net9.0", "net10.0" })
            {
                Assert.Contains($"lib/{framework}/Dapr.Messaging.Abstractions.dll", entries);
                Assert.Contains($"lib/{framework}/Dapr.Messaging.dll", entries);
            }

            using var runtimeStream = package.GetEntry("lib/net10.0/Dapr.Messaging.dll")!.Open();
            var runtimeAssembly = Assembly.Load(ReadAllBytes(runtimeStream));
            Assert.NotNull(runtimeAssembly.GetType("Dapr.Messaging.DaprMessagingRegistration", throwOnError: true));

            var analyzerPath = "analyzers/dotnet/cs/Dapr.Messaging.Analyzers.dll";
            var generatorPath = "analyzers/dotnet/cs/Dapr.Messaging.Generators.dll";
            Assert.Contains(analyzerPath, entries);
            Assert.Contains(generatorPath, entries);

            using var analyzerStream = package.GetEntry(analyzerPath)!.Open();
            using var generatorStream = package.GetEntry(generatorPath)!.Open();
            var analyzerAssembly = Assembly.Load(ReadAllBytes(analyzerStream));
            var generatorAssembly = Assembly.Load(ReadAllBytes(generatorStream));

            var analyzerType = analyzerAssembly.GetType(
                "Dapr.Messaging.Analyzers.MissingMapDaprAppCallbackAnalyzer",
                throwOnError: true)!;
            Assert.True(typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType));
            Assert.NotNull(Activator.CreateInstance(analyzerType));

            var codeFixType = analyzerAssembly.GetType(
                "Dapr.Messaging.Analyzers.MapDaprAppCallbackCodeFixProvider",
                throwOnError: true)!;
            var codeFix = Assert.IsAssignableFrom<CodeFixProvider>(
                Activator.CreateInstance(codeFixType));
            Assert.Contains("DAPR1613", codeFix.FixableDiagnosticIds);

            var generatorType = generatorAssembly.GetType(
                "Dapr.Messaging.Generators.TopicHandlerSourceGenerator",
                throwOnError: true)!;
            Assert.True(typeof(IIncrementalGenerator).IsAssignableFrom(generatorType));
            Assert.NotNull(Activator.CreateInstance(generatorType));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static (int ExitCode, string Output) PackDaprMessaging(string repoRoot, string outputDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("pack");
        startInfo.ArgumentList.Add(Path.Combine("src", "Dapr.Messaging", "Dapr.Messaging.csproj"));
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);
        // No --no-restore: CI only restores the test project's dependency graph before running
        // tests, so the bundled src projects may not have assets files yet.

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet pack.");
        var output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string? GetPropertyValue(IEnumerable<XElement> elements, string propertyName) =>
        elements.SingleOrDefault(element => element.Name.LocalName == propertyName)?.Value;

    private static bool GetBooleanPropertyValue(IEnumerable<XElement> elements, string propertyName, bool defaultValue)
    {
        var value = GetPropertyValue(elements, propertyName);

        return value is null
            ? defaultValue
            : bool.Parse(value);
    }

    private static string[] GetItemIncludes(IEnumerable<XElement> elements, string itemName) =>
        GetItemIncludes(elements, element => element.Name.LocalName == itemName);

    private static string[] GetItemIncludes(IEnumerable<XElement> elements, Func<XElement, bool> predicate) =>
        elements
            .Where(predicate)
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .ToArray();

    private static string[] GetProjectPathMentions(IEnumerable<XElement> elements) =>
        elements
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Name.LocalName is "Include" or "Projects")
            .Select(attribute => attribute.Value)
            .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => value.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static string GetProjectFileName(string msbuildPath) =>
        msbuildPath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();

    private static void AssertTargetsTfmSpecificPackageFiles(IEnumerable<XElement> elements)
    {
        var packagePaths = elements
            .Where(element => element.Name.LocalName == "PackagePath")
            .Select(element => element.Value)
            .ToArray();

        Assert.Contains(packagePaths, path => path.Contains("lib/", StringComparison.OrdinalIgnoreCase) || path.Contains("lib\\", StringComparison.OrdinalIgnoreCase));
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

    public sealed record PackageContractCase(
        string PackageId,
        string ProjectPath,
        string[] RequiredProjectReferences,
        string[] RequiredPackageReferences,
        string[] RequiredBundledProjects,
        string[] RequiredAnalyzerProjects,
        bool IncludeBuildOutput)
    {
        public override string ToString() => PackageId;
    }
}
#endif
