using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Dapr.Actors.Next.MetaConsumerSmoke.Test;

public sealed class MetaConsumerSmokeTests
{
    [Fact]
    public async Task Generator_flows_through_meta_package()
    {
        var root = FindRepoRoot();
        var packages = await PackMetaClosureAsync(root);
        var version = FindMetaPackageVersion(packages);
        var consumer = CreateConsumerDirectory();
        try
        {
            await WriteConsumerProjectAsync(consumer, packages, version);
            await File.WriteAllTextAsync(Path.Combine(consumer, "Program.cs"), """
                using Dapr.Actors.Next.Abstractions;
                using Dapr.Actors.Next.Abstractions.Attributes;
                using Dapr.Actors.Next.Abstractions.Options;
                using Dapr.Actors.Next.Abstractions.Registry;
                using Microsoft.Extensions.DependencyInjection;

                var services = new ServiceCollection();
                services.AddDaprActors(_ => { });
                using var provider = services.BuildServiceProvider();
                var registry = provider.GetRequiredService<IActorRegistry>();
                return registry.TryGet("SmokeActor", out var descriptor) && descriptor.InterfaceType == typeof(ISmokeActor) ? 0 : 2;

                [GenerateActorClient]
                public interface ISmokeActor : IActor
                {
                    Task Ping(CancellationToken cancellationToken = default);
                }

                [DaprActor("SmokeActor")]
                public sealed class SmokeActor : Actor, ISmokeActor
                {
                    protected override ActorId Id => ActorId.Create("unused");
                    protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotSupportedException();
                    public Task Ping(CancellationToken cancellationToken = default) => Task.CompletedTask;
                }
                """);

            var result = await RunDotnetAsync("run --project Consumer.csproj -v:minimal /nr:false", consumer, TimeSpan.FromSeconds(90));

            Assert.True(result.ExitCode == 0, result.Output);
        }
        finally
        {
            Directory.Delete(consumer, recursive: true);
        }
    }

    [Fact]
    public async Task Analyzer_flows_through_meta_package()
    {
        var root = FindRepoRoot();
        var packages = await PackMetaClosureAsync(root);
        var version = FindMetaPackageVersion(packages);
        var temp = CreateConsumerDirectory();
        try
        {
            await WriteConsumerProjectAsync(temp, packages, version);
            await File.WriteAllTextAsync(Path.Combine(temp, "Program.cs"), "return 0;");
            await File.WriteAllTextAsync(Path.Combine(temp, "BadActor.cs"), """
                using Dapr.Actors.Next.Abstractions;
                using Dapr.Actors.Next.Abstractions.Attributes;
                [GenerateActorClient]
                public interface IBadActor : IActor
                {
                    Task Bad();
                }
                [DaprActor("BadActor")]
                public sealed class BadActor : Actor, IBadActor
                {
                    protected override ActorId Id => ActorId.Create("bad");
                    protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotSupportedException();
                    public Task Bad() => Task.Run(() => { });
                }
                """);

            var result = await RunDotnetAsync("build Consumer.csproj -v:minimal /m:1 /nr:false", temp, TimeSpan.FromSeconds(90));

            Assert.NotEqual(0, result.ExitCode);
            Assert.True(result.Output.Contains("DAPR1411", StringComparison.Ordinal), result.Output);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "all.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }

    private static async Task<string> PackMetaClosureAsync(string root)
    {
        var packages = Path.Combine(Path.GetTempPath(), "dapr-actors-next-packages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packages);
        var packageVersion = "999.0.0-local." + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var projects = new[]
        {
            "src/Dapr.Protos/Dapr.Protos.csproj",
            "src/Dapr.Common/Dapr.Common.csproj",
            "src/Dapr.Actors.Next.Abstractions/Dapr.Actors.Next.Abstractions.csproj",
            "src/Dapr.Actors.Next.Core/Dapr.Actors.Next.Core.csproj",
            "src/Dapr.Messaging/Dapr.Messaging.csproj",
            "src/Dapr.Actors.Next.StateMachine/Dapr.Actors.Next.StateMachine.csproj",
            "src/Dapr.Actors.Next.Streams/Dapr.Actors.Next.Streams.csproj",
            "src/Dapr.Actors.Next.Interpreted/Dapr.Actors.Next.Interpreted.csproj",
            "src/Dapr.Actors.Next.Testing/Dapr.Actors.Next.Testing.csproj",
            "src/Dapr.Actors.Next/Dapr.Actors.Next.csproj",
        };

        foreach (var project in projects)
        {
            var result = await RunDotnetAsync($"pack {project} -c Debug -o \"{packages}\" --no-build /p:IncludeSymbols=false /p:Version={packageVersion} /p:PackageVersion={packageVersion} /p:MinVerVersionOverride={packageVersion} /nr:false", root, TimeSpan.FromSeconds(90));
            Assert.True(result.ExitCode == 0, result.Output);
        }

        return packages;
    }

    private static string CreateConsumerDirectory()
    {
        var temp = Path.Combine(Path.GetTempPath(), "dapr-actors-next-meta-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }

    private static Task WriteConsumerProjectAsync(string directory, string packages, string version) =>
        File.WriteAllTextAsync(Path.Combine(directory, "Consumer.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <RestoreAdditionalProjectSources>{{packages}}</RestoreAdditionalProjectSources>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Dapr.Actors.Next" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """);

    private static string FindMetaPackageVersion(string packages)
    {
        var file = Directory.GetFiles(packages, "Dapr.Actors.Next.*.nupkg")
            .Select(Path.GetFileName)
            .Where(name => name is not null && Regex.IsMatch(name, @"^Dapr\.Actors\.Next\.\d", RegexOptions.CultureInvariant))
            .Order(StringComparer.Ordinal)
            .Last();
        return file!["Dapr.Actors.Next.".Length..^".nupkg".Length];
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(string arguments, string workingDirectory, TimeSpan timeout)
    {
        var start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet process could not be started.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        if (!await WaitForExitAsync(process, timeout))
        {
            process.Kill(entireProcessTree: true);
            return (-1, output + error + "Timed out.");
        }

        return (process.ExitCode, output + error);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        var waitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(waitTask, Task.Delay(timeout));
        return ReferenceEquals(completed, waitTask) && process.HasExited;
    }
}
