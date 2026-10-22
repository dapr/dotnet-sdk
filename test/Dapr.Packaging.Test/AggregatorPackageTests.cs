using System.Xml.Linq;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
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
            PackageId: "Dapr.Vector",
            ProjectPath: Path.Combine("src", "Dapr.Vector", "Dapr.Vector.csproj"),
            RequiredProjectReferences: ["Dapr.Common.csproj", "Dapr.Protos.csproj"],
            RequiredPackageReferences:
            [
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredBundledProjects: ["Dapr.Vector.Abstractions.csproj", "Dapr.Vector.Runtime.csproj"],
            RequiredAnalyzerProjects: ["Dapr.Vector.Generators.csproj"],
            IncludeBuildOutput: false),

        new PackageContractCase(
            PackageId: "Dapr.Search",
            ProjectPath: Path.Combine("src", "Dapr.Search", "Dapr.Search.csproj"),
            RequiredProjectReferences: ["Dapr.Common.csproj", "Dapr.Protos.csproj"],
            RequiredPackageReferences:
            [
                "Google.Protobuf",
                "Grpc.Net.Client",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Http",
            ],
            RequiredBundledProjects: ["Dapr.Search.Abstractions.csproj", "Dapr.Search.Runtime.csproj"],
            RequiredAnalyzerProjects: ["Dapr.Search.Generators.csproj"],
            IncludeBuildOutput: false),

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
            PackageId: "Dapr.BinaryStore",
            ProjectPath: Path.Combine("src", "Dapr.BinaryStore", "Dapr.BinaryStore.csproj"),
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
                "Dapr.BinaryStore.Abstractions.csproj",
                "Dapr.BinaryStore.Runtime.csproj",
            ],
            RequiredAnalyzerProjects:
            [
                "Dapr.BinaryStore.Generators.csproj",
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
    [InlineData("Dapr.BinaryStore", null)]
    [InlineData("Dapr.BinaryStore", "false")]
    [InlineData("Dapr.BinaryStore", "true")]
    [InlineData("Dapr.Messaging", null)]
    [InlineData("Dapr.Messaging", "false")]
    [InlineData("Dapr.Messaging", "true")]
    public async Task AggregatorPackage_OnlyPreviewBuildsAllowPrereleaseDependencyWarnings(
        string projectName, string? enableNet11)
    {
        // Pack runs in the outer build, where TargetFramework is empty, in a separate dotnet process.
        var result = await RunDotNetAsync(FindRepoRoot(), "msbuild",
            Path.Combine("src", projectName, projectName + ".csproj"),
            $"-p:DaprEnableNet11={enableNet11}",
            "-getProperty:TargetFramework,TreatWarningsAsErrors,WarningsNotAsErrors,NoWarn");
        Assert.True(result.ExitCode == 0, result.Output);

        using var evaluation = JsonDocument.Parse(result.Output);
        var properties = evaluation.RootElement.GetProperty("Properties");
        Assert.Equal("", properties.GetProperty("TargetFramework").GetString());
        Assert.Equal("true", properties.GetProperty("TreatWarningsAsErrors").GetString());

        var warningsNotAsErrors = properties.GetProperty("WarningsNotAsErrors").GetString()!
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(enableNet11 == "true", warningsNotAsErrors.Contains("NU5104"));
        Assert.Contains("NU1901", warningsNotAsErrors);
        Assert.Contains("NU1902", warningsNotAsErrors);
        Assert.Contains("NU1903", warningsNotAsErrors);
        Assert.Contains("NU1904", warningsNotAsErrors);
        Assert.DoesNotContain("NU5104", properties.GetProperty("NoWarn").GetString()!
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

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
        var bundledProjectReferences = GetItemIncludes(
            elements,
            element => element.Name.LocalName.EndsWith("ChildLib", StringComparison.Ordinal));
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

    [Theory]
    [InlineData("Search")]
    [InlineData("Vector")]
    public async Task BuildingBlockPackage_BundlesGeneratorAndRunsPackageOnlyConsumer(string buildingBlock)
    {
        var repoRoot = FindRepoRoot();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dapr-{buildingBlock}-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            var version = $"0.0.0-{buildingBlock.ToLowerInvariant()}-smoke.{Guid.NewGuid():N}";
            foreach (var project in new[] { "Dapr.Protos", "Dapr.Common", $"Dapr.{buildingBlock}" })
            {
                var result = await RunDotNetAsync(repoRoot, "pack",
                    Path.Combine("src", project, project + ".csproj"),
                    "--configuration", "Release", "--output", outputDirectory,
                    $"/p:Version={version}", $"/p:PackageVersion={version}", $"/p:MinVerVersionOverride={version}",
                    "/nr:false", "/p:UseSharedCompilation=false");
                Assert.True(result.ExitCode == 0, result.Output);
            }
            var packagePath = Assert.Single(Directory.EnumerateFiles(outputDirectory, $"Dapr.{buildingBlock}.*.nupkg"));
            using var package = ZipFile.OpenRead(packagePath);
            var entries = package.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var framework in new[] { "net8.0", "net9.0", "net10.0" })
            {
                Assert.Contains($"lib/{framework}/Dapr.{buildingBlock}.Abstractions.dll", entries);
                Assert.Contains($"lib/{framework}/Dapr.{buildingBlock}.Runtime.dll", entries);
                Assert.Contains($"lib/{framework}/Dapr.{buildingBlock}.Abstractions.xml", entries);
                Assert.Contains($"lib/{framework}/Dapr.{buildingBlock}.Runtime.xml", entries);
            }
            Assert.Single(package.Entries, entry => entry.FullName == $"analyzers/dotnet/cs/Dapr.{buildingBlock}.Generators.dll");
            Assert.DoesNotContain(entries, name => name.StartsWith("lib/", StringComparison.Ordinal) && name.Contains("Generators", StringComparison.Ordinal));
            Assert.DoesNotContain(entries, name => name.Contains("Microsoft.CodeAnalysis", StringComparison.Ordinal));
            using var manifestStream = package.GetEntry($"Dapr.{buildingBlock}.nuspec")!.Open();
            var dependencies = XDocument.Load(manifestStream).Descendants()
                .Where(element => element.Name.LocalName == "dependency")
                .Select(element => (string?)element.Attribute("id")).ToArray();
            Assert.Contains("Dapr.Common", dependencies);
            Assert.Contains("Dapr.Protos", dependencies);
            Assert.Contains("Grpc.Net.Client", dependencies);
            Assert.Contains("Microsoft.Extensions.Http", dependencies);
            Assert.DoesNotContain($"Dapr.{buildingBlock}.Abstractions", dependencies);
            Assert.DoesNotContain($"Dapr.{buildingBlock}.Runtime", dependencies);
            Assert.DoesNotContain($"Dapr.{buildingBlock}.Generators", dependencies);
            Assert.DoesNotContain(dependencies, name => name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));

            if (buildingBlock == "Search")
                await VerifySearchPackageConsumerAsync(outputDirectory, version);
            else
                await VerifyVectorPackageConsumerAsync(outputDirectory, version);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static async Task VerifyVectorPackageConsumerAsync(string feed, string version)
    {
        var consumer = Path.Combine(feed, "consumer");
        Directory.CreateDirectory(consumer);
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(consumer, "Consumer.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
                <Nullable>enable</Nullable>
                <ImplicitUsings>disable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Dapr.Vector" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """, ct);
        await File.WriteAllTextAsync(Path.Combine(consumer, "Program.cs"), """
            using System;
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Vector;
            using Dapr.Vector.Extensions;
            using Microsoft.Extensions.DependencyInjection;

            [VectorCollection("packaged-store", "products")]
            public partial interface IProductVectors : IDaprVectorCollection<Product>;
            public sealed record Product(string Title);
            [JsonSerializable(typeof(Product))]
            internal partial class ProductJsonContext : JsonSerializerContext;

            internal static class Program
            {
                private static async Task Main()
                {
                    var services = new ServiceCollection();
                    services.AddDaprVectorClient((_, builder) =>
                        builder.UseJsonSerializationOptions(ProductJsonContext.Default.Options)).AddProductVectors();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateScopes = true, ValidateOnBuild = true
                    });
                    var collection = provider.GetRequiredService<IProductVectors>();
                    if (!ReferenceEquals(collection, provider.GetRequiredService<IProductVectors>()) || collection is IDisposable)
                        throw new InvalidOperationException("Generated collection lifetime or ownership is incorrect.");
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    // Serialization occurs before the RPC cancellation check; this also exercises the
                    // package's JSON context support with reflection disabled, without needing a sidecar.
                    try
                    {
                        await collection.UpsertVectorsAsync(
                            new[] { new VectorRecord<Product>("1", new float[] { 1, 0, 0 }, new Product("Dapr")) },
                            cancellationToken: cancellation.Token);
                        throw new InvalidOperationException("Cancellation was not propagated.");
                    }
                    catch (OperationCanceledException error) when (error.CancellationToken == cancellation.Token) { }
                    Console.WriteLine("PACKAGED_VECTOR_GENERATOR_OK");
                }
            }
            """, ct);
        await RunPackageConsumerAsync(consumer, feed, version, "Dapr.Vector", "PACKAGED_VECTOR_GENERATOR_OK");
    }

    private static async Task VerifySearchPackageConsumerAsync(string feed, string version)
    {
        var consumer = Path.Combine(feed, "consumer");
        Directory.CreateDirectory(consumer);
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(consumer, "Consumer.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
                <Nullable>enable</Nullable>
                <ImplicitUsings>disable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Dapr.Search" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """, ct);
        await File.WriteAllTextAsync(Path.Combine(consumer, "Program.cs"), """
            using System;
            using System.Collections.Generic;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Search;
            using Dapr.Search.Extensions;
            using Microsoft.Extensions.DependencyInjection;

            [SearchIndex("packaged-store", "products")]
            public partial interface IProductSearch : IDaprSearchIndex<Product>;
            public sealed record Product(string Title);

            [JsonSerializable(typeof(Product))]
            internal partial class ProductJsonContext : JsonSerializerContext;

            internal static class Program
            {
                private static async Task Main()
                {
                    using var client = new DaprSearchClientBuilder()
                        .UseJsonSerializationOptions(ProductJsonContext.Default.Options).Build();
                    var services = new ServiceCollection();
                    services.AddDaprSearchClient().AddProductSearch();
                    using var recordingClient = new RecordingClient();
                    services.AddSingleton<DaprSearchClient>(recordingClient);
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateScopes = true, ValidateOnBuild = true
                    });
                    var index = provider.GetRequiredService<IProductSearch>();
                    using var cancellation = new CancellationTokenSource();
                    var result = await index.SearchAsync(new SearchQuery { Text = "Dapr", IncludeContent = true },
                        new Dictionary<string, string> { ["source"] = "package" }, cancellation.Token);
                    if (result.Hits.Count != 1 || result.Hits[0].Document.Content != new Product("Dapr") ||
                        recordingClient.Token != cancellation.Token ||
                        !ReferenceEquals(index, provider.GetRequiredService<IProductSearch>()) ||
                        index is IDisposable)
                        throw new InvalidOperationException("Generated index did not preserve its contract.");
                    Console.WriteLine("PACKAGED_SEARCH_GENERATOR_OK");
                }
            }

            internal sealed class RecordingClient : DaprSearchClient
            {
                public CancellationToken Token { get; private set; }

                public override Task<SearchResult<T>> SearchAsync<T>(string storeName, string index,
                    SearchQuery? query = null, IReadOnlyDictionary<string, string>? metadata = null,
                    CancellationToken cancellationToken = default)
                {
                    if (storeName != "packaged-store" || index != "products" || typeof(T) != typeof(Product) ||
                        query?.Text != "Dapr" || query.IncludeContent != true || metadata?["source"] != "package")
                        throw new InvalidOperationException("Generated index did not forward the request.");
                    Token = cancellationToken;
                    var product = JsonSerializer.Deserialize<T>("{\"Title\":\"Dapr\"}", ProductJsonContext.Default.Options);
                    return Task.FromResult(new SearchResult<T>(
                        new[] { new SearchHit<T>(new SearchDocument<T>("1", product), 1, new Dictionary<string, string>()) },
                        1, TotalHitsRelation.Exact, null));
                }

                public override Task CreateIndexAsync(string storeName, string index,
                    IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) =>
                    throw new NotSupportedException();
                public override Task<SearchIndex> GetIndexAsync(string storeName, string index,
                    IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) =>
                    throw new NotSupportedException();
                public override Task<IReadOnlyList<string>> ListIndexesAsync(string storeName,
                    IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) =>
                    throw new NotSupportedException();
                public override Task DeleteIndexAsync(string storeName, string index,
                    IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) =>
                    throw new NotSupportedException();
                public override Task<IndexDocumentsResult> IndexDocumentsAsync<T>(string storeName, string index,
                    IReadOnlyList<SearchDocument<T>> documents, IndexingOptions? options = null,
                    IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) =>
                    throw new NotSupportedException();
                public override Task<IReadOnlyList<SearchDocument<T>>> GetDocumentsAsync<T>(string storeName, string index,
                    IReadOnlyList<string> ids, bool? includeContent = null,
                    IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) =>
                    throw new NotSupportedException();
                public override Task<IndexAck> DeleteDocumentsAsync(string storeName, string index,
                    IReadOnlyList<string> ids, IndexingOptions? options = null,
                    IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default) =>
                    throw new NotSupportedException();
            }
            """, ct);

        await RunPackageConsumerAsync(consumer, feed, version, "Dapr.Search", "PACKAGED_SEARCH_GENERATOR_OK");
    }

    private static async Task RunPackageConsumerAsync(string consumer, string feed, string version,
        string packageId, string successMarker)
    {
        var ct = TestContext.Current.CancellationToken;
        new XDocument(new XElement("configuration",
            new XElement("packageSources",
                new XElement("clear"),
                new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feed)),
                new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json")))))
            .Save(Path.Combine(consumer, "NuGet.Config"));
        var restore = await RunDotNetAsync(consumer, "restore", "Consumer.csproj", "--configfile", "NuGet.Config",
            "--packages", Path.Combine(feed, "packages"));
        Assert.True(restore.ExitCode == 0, restore.Output);
        using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(consumer, "obj", "project.assets.json"), ct));
        Assert.True(assets.RootElement.GetProperty("libraries").TryGetProperty($"{packageId}/{version}", out _));
        foreach (var framework in new[] { "net8.0", "net9.0", "net10.0" })
        {
            var run = await RunDotNetAsync(consumer, "run", "--no-restore", "--framework", framework,
                "--configuration", "Release", "/p:UseSharedCompilation=false");
            Assert.True(run.ExitCode == 0, run.Output);
            Assert.Contains(successMarker, run.Output);
            var deps = await File.ReadAllTextAsync(Path.Combine(consumer, "bin", "Release", framework, "Consumer.deps.json"), ct);
            Assert.DoesNotContain($"{packageId}.Generators", deps);
            Assert.DoesNotContain("Microsoft.CodeAnalysis", deps);
        }
    }

    [Fact]
    public async Task DaprBinaryStorePackage_RestoresAndBundlesGeneratorPrivately()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine("src", "Dapr.BinaryStore", "Dapr.BinaryStore.csproj");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dapr-binary-store-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Inspect the restore graph so existing generator assets cannot mask a missing reference.
            var graphPath = Path.Combine(outputDirectory, "restore-graph.json");
            var graphResult = await RunDotNetAsync(repoRoot, "msbuild", projectPath,
                "-t:GenerateRestoreGraphFile", $"-p:RestoreGraphOutputPath={graphPath}");
            Assert.True(graphResult.ExitCode == 0, graphResult.Output);

            using var graph = JsonDocument.Parse(File.ReadAllText(graphPath));
            var restoredProjects = graph.RootElement.GetProperty("projects").EnumerateObject()
                .Select(project => GetProjectFileName(project.Name));
            Assert.Contains("Dapr.BinaryStore.Generators.csproj", restoredProjects);

            var restoreResult = await RunDotNetAsync(repoRoot, "restore", projectPath);
            Assert.True(restoreResult.ExitCode == 0, restoreResult.Output);

            var packResult = await RunDotNetAsync(repoRoot, "pack", projectPath, "--no-restore",
                "--configuration", "Release", "--output", outputDirectory);
            Assert.True(packResult.ExitCode == 0, packResult.Output);

            var packagePath = Assert.Single(Directory.EnumerateFiles(outputDirectory, "Dapr.BinaryStore.*.nupkg"));
            using var package = ZipFile.OpenRead(packagePath);
            var entries = package.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var framework in new[] { "net8.0", "net9.0", "net10.0" })
            {
                Assert.Contains($"lib/{framework}/Dapr.BinaryStore.Abstractions.dll", entries);
                Assert.Contains($"lib/{framework}/Dapr.BinaryStore.Runtime.dll", entries);
            }

            Assert.Contains("analyzers/dotnet/cs/Dapr.BinaryStore.Generators.dll", entries);

            using var manifestStream = package.GetEntry("Dapr.BinaryStore.nuspec")!.Open();
            var manifest = XDocument.Load(manifestStream);
            var dependencies = manifest.Descendants()
                .Where(element => element.Name.LocalName == "dependency")
                .Select(element => (string?)element.Attribute("id"))
                .ToArray();
            Assert.Contains("Dapr.Common", dependencies);
            Assert.DoesNotContain("Dapr.BinaryStore.Generators", dependencies);
            Assert.DoesNotContain("Dapr.BinaryStore.Abstractions", dependencies);
            Assert.DoesNotContain("Dapr.BinaryStore.Runtime", dependencies);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DaprMessagingPackage_ContainsRuntimeAnalyzersCodeFixAndSourceGenerator()
    {
        var repoRoot = FindRepoRoot();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dapr-messaging-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // CI only restores the test project's dependency graph, so pack must restore its own graph.
            var result = await RunDotNetAsync(repoRoot, "pack",
                Path.Combine("src", "Dapr.Messaging", "Dapr.Messaging.csproj"),
                "--configuration", "Release", "--output", outputDirectory,
                "/nr:false", "/p:UseSharedCompilation=false");
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

    private static async Task<(int ExitCode, string Output)> RunDotNetAsync(
        string repoRoot,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(process.WaitForExitAsync(), stdOutTask, stdErrTask);
        var output = (await stdOutTask) + Environment.NewLine + (await stdErrTask);
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
