using System.Xml.Linq;

namespace Dapr.Packaging.Test;

#if NET10_0
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
            Assert.Contains(projectReferences, reference => Path.GetFileName(reference) == requiredProjectReference);
        }

        foreach (var requiredPackageReference in package.RequiredPackageReferences)
        {
            Assert.Contains(requiredPackageReference, packageReferences);
        }

        foreach (var requiredBundledProject in package.RequiredBundledProjects)
        {
            Assert.Contains(bundledProjectReferences, reference => Path.GetFileName(reference) == requiredBundledProject);
        }

        foreach (var requiredAnalyzerProject in package.RequiredAnalyzerProjects)
        {
            Assert.Contains(projectPathMentions, reference => Path.GetFileName(reference) == requiredAnalyzerProject);
        }

        AssertTargetsTfmSpecificPackageFiles(elements);
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
