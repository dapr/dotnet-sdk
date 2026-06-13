// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#if NET10_0
using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
#endif

namespace Dapr.IntegrationTest.Workflow.Versioning;

public sealed class RegistrationOrderRegressionIntegrationTests
{
    private const string CanonicalWorkflowName = "RegistrationOrderRegressionWorkflow";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedVersioningRegistrationsShouldOverridePlainAutoRegistrationForCanonicalName(bool configureVersioningFirst)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        if (configureVersioningFirst)
        {
            services.AddDaprWorkflowVersioning();
            services.AddDaprWorkflowBuilder(configureRuntime: _ => { });
        }
        else
        {
            services.AddDaprWorkflowBuilder(configureRuntime: _ => { });
            services.AddDaprWorkflowVersioning();
        }

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IWorkflowsFactory>();

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier(CanonicalWorkflowName), provider, out var workflow, out var activationException));
        Assert.Null(activationException);
        Assert.NotNull(workflow);
        Assert.IsType<RegistrationOrderRegressionWorkflowV3>(workflow);

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier(nameof(RegistrationOrderRegressionWorkflow)), provider, out var simpleNameWorkflow, out _));
        Assert.NotNull(simpleNameWorkflow);
        Assert.IsType<RegistrationOrderRegressionWorkflowV3>(simpleNameWorkflow);
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task SchedulingCanonicalWorkflowNameShouldRunLatestGeneratedVersion()
    {
        var instanceId = Guid.NewGuid().ToString("N");
        var appId = $"workflow-versioning-order-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions().WithAppId(appId);
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-versioning-order");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        await using var app = await StartVersionedAppAsync(componentsDir, environment, options);
        using var scope = app.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(CanonicalWorkflowName, instanceId, "runtime");
        using var completionCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var state = await client.WaitForWorkflowCompletionAsync(instanceId, cancellation: completionCts.Token);

        Assert.Equal(WorkflowRuntimeStatus.Completed, state.RuntimeStatus);
        Assert.Equal("v3:runtime", state.ReadOutputAs<string>());
    }

#if NET10_0
    [Fact]
    public void PackedDaprWorkflowPackageShouldExposeVersioningAndRouteCanonicalNameToLatest()
    {
        var repoRoot = FindRepoRoot();
        var packageFeed = Path.Combine(Path.GetTempPath(), $"dapr-workflow-package-feed-{Guid.NewGuid():N}");
        var consumerDir = Path.Combine(Path.GetTempPath(), $"dapr-workflow-package-consumer-{Guid.NewGuid():N}");
        var packageCache = Path.Combine(Path.GetTempPath(), $"dapr-workflow-package-cache-{Guid.NewGuid():N}");

        Directory.CreateDirectory(packageFeed);
        Directory.CreateDirectory(consumerDir);
        Directory.CreateDirectory(packageCache);

        try
        {
            var packageVersion = $"1.18.1-regression.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            RunDotNet(repoRoot, "pack", "src\\Dapr.Workflow\\Dapr.Workflow.csproj", "--no-restore", "-c", "Debug",
                "-p:TargetFrameworks=net8.0", "-p:BuildInParallel=false", $"-p:MinVerVersionOverride={packageVersion}",
                $"-p:PackageOutputPath={packageFeed}");

            var packagePath = Directory.GetFiles(packageFeed, "Dapr.Workflow.*.nupkg")
                .Single(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(packageVersion, ReadPackageVersion(packagePath));
            AssertPackageContainsVersioningAssets(packagePath);

            File.WriteAllText(Path.Combine(consumerDir, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packageFeed}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                </configuration>
                """);

            File.WriteAllText(Path.Combine(consumerDir, "PackageConsumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net8.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Dapr.Workflow" Version="{packageVersion}" />
                    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.8" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.8" />
                  </ItemGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(consumerDir, "Program.cs"), """
                using Dapr.Workflow;
                using Dapr.Workflow.Abstractions;
                using Dapr.Workflow.Versioning;
                using Dapr.Workflow.Worker;
                using Microsoft.Extensions.DependencyInjection;

                const string canonicalName = "PackageRegressionWorkflow";

                var services = new ServiceCollection();
                services.AddLogging();
                services.AddDaprWorkflow();
                services.AddDaprWorkflowVersioning();

                using var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IWorkflowsFactory>();

                if (!factory.TryCreateWorkflow(new TaskIdentifier(canonicalName), provider, out var workflow, out var activationException))
                {
                    Console.Error.WriteLine($"Could not create canonical workflow. Activation exception: {activationException}");
                    return 2;
                }

                if (workflow is not PackageRegressionWorkflowV3)
                {
                    Console.Error.WriteLine($"Expected PackageRegressionWorkflowV3, got {workflow?.GetType().FullName ?? "<null>"}.");
                    return 3;
                }

                return 0;

                internal sealed class PackageRegressionWorkflow : Workflow<string, string>
                {
                    public override Task<string> RunAsync(WorkflowContext context, string input)
                    {
                        return Task.FromResult($"v1:{input}");
                    }
                }

                internal sealed class PackageRegressionWorkflowV2 : Workflow<string, string>
                {
                    public override Task<string> RunAsync(WorkflowContext context, string input)
                    {
                        return Task.FromResult($"v2:{input}");
                    }
                }

                internal sealed class PackageRegressionWorkflowV3 : Workflow<string, string>
                {
                    public override Task<string> RunAsync(WorkflowContext context, string input)
                    {
                        return Task.FromResult($"v3:{input}");
                    }
                }
                """);

            RunDotNet(
                consumerDir,
                new Dictionary<string, string?> { ["NUGET_PACKAGES"] = packageCache },
                "run", "--no-launch-profile", "--configuration", "Debug");
        }
        finally
        {
            TryDeleteDirectory(packageFeed);
            TryDeleteDirectory(consumerDir);
            TryDeleteDirectory(packageCache);
        }
    }
#endif

    private static async Task<DaprTestApplication> StartVersionedAppAsync(
        string componentsDir,
        DaprTestEnvironment environment,
        DaprRuntimeOptions options)
    {
        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .WithOptions(options)
            .BuildWorkflow();

        var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: _ => { },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                        {
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                        }
                    });

                builder.Services.AddDaprWorkflowVersioning();
            })
            .BuildAndStartAsync();

        await WaitForSidecarAsync(app, TimeSpan.FromMinutes(1));
        return app;
    }

    private static async Task WaitForSidecarAsync(DaprTestApplication app, TimeSpan timeout)
    {
        using var scope = app.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var stopAt = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < stopAt)
        {
            try
            {
                await client.GetWorkflowStateAsync($"warmup-{Guid.NewGuid():N}", getInputsAndOutputs: false);
                return;
            }
            catch (RpcException ex) when (IsTransientRpc(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        Assert.Fail("Timed out waiting for Dapr sidecar readiness.");
    }

    private static bool IsTransientRpc(RpcException ex) =>
        ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded;

#if NET10_0
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var workflowProjectPath = Path.Combine(current.FullName, "src", "Dapr.Workflow", "Dapr.Workflow.csproj");
            if (File.Exists(workflowProjectPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static string ReadPackageVersion(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var nuspec = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = document.Root?.Name.Namespace ?? XNamespace.None;
        return document.Root?.Element(ns + "metadata")?.Element(ns + "version")?.Value
            ?? throw new InvalidOperationException($"Could not read package version from '{packagePath}'.");
    }

    private static void AssertPackageContainsVersioningAssets(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("analyzers/dotnet/cs/Dapr.Workflow.Versioning.Generators.dll", entries);
        Assert.Contains("lib/net8.0/Dapr.Workflow.Versioning.Abstractions.dll", entries);
        Assert.Contains("lib/net8.0/Dapr.Workflow.Versioning.Runtime.dll", entries);
    }

    private static void RunDotNet(string workingDirectory, params string[] arguments) =>
        RunDotNet(workingDirectory, environment: null, arguments);

    private static void RunDotNet(string workingDirectory, IReadOnlyDictionary<string, string?>? environment, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for diagnostics-preserving package-consumption tests.
        }
    }
#endif

    [WorkflowVersion(CanonicalName = CanonicalWorkflowName, Version = "1")]
    internal sealed class RegistrationOrderRegressionWorkflow : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input)
        {
            return Task.FromResult($"v1:{input}");
        }
    }

    [WorkflowVersion(CanonicalName = CanonicalWorkflowName, Version = "2")]
    internal sealed class RegistrationOrderRegressionWorkflowV2 : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input)
        {
            return Task.FromResult($"v2:{input}");
        }
    }

    [WorkflowVersion(CanonicalName = CanonicalWorkflowName, Version = "3")]
    internal sealed class RegistrationOrderRegressionWorkflowV3 : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input)
        {
            return Task.FromResult($"v3:{input}");
        }
    }
}
