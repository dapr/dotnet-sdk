using System.Diagnostics;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed record DaprSidecarOptions(string AppId)
{
    public int? AppPort { get; init; }

    public int? DaprGrpcPort { get; init;}

    public int? DaprHttpPort { get; init; }

    public ILoggerFactory? LoggerFactory { get; init; }
}

internal sealed class DaprSidecar : IAsyncDisposable
{
    private readonly Process process;
    private readonly ILogger? logger;

    public DaprSidecar(DaprSidecarOptions options)
    {
        string arguments = $"run --app-id {options.AppId}";

        if (options.DaprGrpcPort is not null)
        {
            arguments += $" --dapr-grpc-port {options.DaprGrpcPort}";
        }

        if (options.DaprHttpPort is not null)
        {
            arguments += $" --dapr-http-port {options.DaprHttpPort}";
        }

        this.process = new Process
        {
            EnableRaisingEvents = false, // ?
            StartInfo =
            {
                Arguments = arguments,
                CreateNoWindow = true,
                FileName = "dapr",
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        if (options.LoggerFactory is not null)
        {
            this.logger = options.LoggerFactory.CreateLogger(options.AppId);

            this.process.StartInfo.RedirectStandardError = true;
            this.process.StartInfo.RedirectStandardOutput = true;

            this.process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    this.logger.LogInformation(args.Data);
                }
            };

            this.process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    this.logger.LogError(args.Data);
                }
            };
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        process.Start();

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        process.Kill(entireProcessTree: true);

        return process.WaitForExitAsync(cancellationToken);
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