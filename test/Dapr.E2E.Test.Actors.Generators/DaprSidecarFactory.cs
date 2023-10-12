// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using System.Diagnostics;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed record DaprSidecarOptions(string AppId)
{
    public int? AppPort { get; init; }

    public int? DaprGrpcPort { get; init;}

    public int? DaprHttpPort { get; init; }

    public ILoggerFactory? LoggerFactory { get; init; }

    public string? LogLevel { get; init; }
}

internal sealed class DaprSidecar : IAsyncDisposable
{
    private const string StartupOutputString = "You're up and running! Dapr logs will appear here.";

    private readonly string appId;
    private readonly Process process;
    private readonly ILogger? logger;
    private readonly TaskCompletionSource<bool> tcs = new();

    public DaprSidecar(DaprSidecarOptions options)
    {
        string arguments = $"run --app-id {options.AppId}";

        if (options.AppPort is not null)
        {
            arguments += $" --app-port {options.AppPort}";
        }

        if (options.DaprGrpcPort is not null)
        {
            arguments += $" --dapr-grpc-port {options.DaprGrpcPort}";
        }

        if (options.DaprHttpPort is not null)
        {
            arguments += $" --dapr-http-port {options.DaprHttpPort}";
        }

        if (options.LogLevel is not null)
        {
            arguments += $" --log-level {options.LogLevel}";
        }

        this.process = new Process
        {
            EnableRaisingEvents = false, // ?
            StartInfo =
            {
                Arguments = arguments,
                CreateNoWindow = true,
                FileName = "dapr",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        if (options.LoggerFactory is not null)
        {
            this.logger = options.LoggerFactory.CreateLogger(options.AppId);
        }

        this.process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                if (args.Data.Contains(StartupOutputString))
                {
                    this.tcs.SetResult(true);
                }

                this.logger?.LogInformation(args.Data);
            }
        };

        this.process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                this.logger?.LogError(args.Data);
            }
        };

        this.appId = options.AppId;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        process.Start();

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        return this.tcs.Task;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var stopProcess = new Process
        {
            StartInfo =
            {
                Arguments = $"stop --app-id {this.appId}",
                CreateNoWindow = true,
                FileName = "dapr",
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        stopProcess.Start();

        await stopProcess.WaitForExitAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }
}

internal sealed class DaprSidecarFactory
{
    public static DaprSidecar Create(DaprSidecarOptions options)
    {
        return new(options);
    }
}