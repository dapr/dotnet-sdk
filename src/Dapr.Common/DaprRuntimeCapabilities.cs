using System.Collections.Concurrent;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1;

namespace Dapr.Common;

/// <summary>
/// Used to determine Dapr runtime capability for fallback purposes by the SDKs.
/// </summary>
internal sealed class DaprRuntimeCapabilities : IDaprRuntimeCapabilities
{
    private static readonly TimeSpan DefaultReflectionTimeout = TimeSpan.FromSeconds(5);

    private readonly ServerReflection.ServerReflectionClient _reflectionClient;
    private readonly TimeSpan _reflectionTimeout;
    private readonly Lazy<Task<HashSet<string>?>> _servicesLookup;
    private readonly ConcurrentDictionary<string, Lazy<Task<HashSet<string>?>>> _methodLookups =
        new(StringComparer.Ordinal);
    private int _reflectionUnavailable;

    public const string Namespace = "dapr.proto.runtime.v1.Dapr";

    /// <summary>
    /// Creates a runtime capability reader for the provided channel.
    /// </summary>
    /// <param name="channel">The <see cref="GrpcChannel"/> to validate with.</param>
    public DaprRuntimeCapabilities(GrpcChannel channel)
        : this(new ServerReflection.ServerReflectionClient(channel), DefaultReflectionTimeout)
    {
    }

    internal DaprRuntimeCapabilities(
        ServerReflection.ServerReflectionClient reflectionClient,
        TimeSpan reflectionTimeout)
    {
        ArgumentNullException.ThrowIfNull(reflectionClient);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reflectionTimeout, TimeSpan.Zero);

        this._reflectionClient = reflectionClient;
        this._reflectionTimeout = reflectionTimeout;
        this._servicesLookup = new Lazy<Task<HashSet<string>?>>(
            QueryServicesWithFallbackAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdocs />
    public async Task<DaprRuntimeSupport> GetMethodSupportAsync(
        string fullyQualifiedMethodName,
        CancellationToken cancellationToken = default)
    {
        var slash = fullyQualifiedMethodName.LastIndexOf('/');
        if (slash <= 0)
        {
            throw new ArgumentException("Expected the form 'package.Service/Method.", nameof(fullyQualifiedMethodName));
        }

        var service = fullyQualifiedMethodName[..slash];
        var method = fullyQualifiedMethodName[(slash + 1)..];

        var methods = await GetMethodsForServiceAsync(service, cancellationToken).ConfigureAwait(false);
        return methods is null
            ? DaprRuntimeSupport.Unknown
            : methods.Contains(method)
                ? DaprRuntimeSupport.Supported
                : DaprRuntimeSupport.Unsupported;
    }

    /// <inheritdocs />
    public async Task<DaprRuntimeSupport> GetServiceSupportAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var services = await GetServicesAsync(cancellationToken).ConfigureAwait(false);
        return services is null
            ? DaprRuntimeSupport.Unknown
            : services.Contains(serviceName)
                ? DaprRuntimeSupport.Supported
                : DaprRuntimeSupport.Unsupported;
    }

    private Task<HashSet<string>?> GetServicesAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _reflectionUnavailable) != 0)
        {
            return Task.FromResult<HashSet<string>?>(null);
        }

        return _servicesLookup.Value.WaitAsync(cancellationToken);
    }

    private Task<HashSet<string>?> GetMethodsForServiceAsync(string serviceName,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _reflectionUnavailable) != 0)
        {
            return Task.FromResult<HashSet<string>?>(null);
        }

        var lookup = _methodLookups.GetOrAdd(
            serviceName,
            static (name, capabilities) => new Lazy<Task<HashSet<string>?>>(
                () => capabilities.QueryMethodsWithFallbackAsync(name),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this);

        return lookup.Value.WaitAsync(cancellationToken);
    }

    private Task<HashSet<string>?> QueryServicesWithFallbackAsync()
    {
        return ExecuteReflectionQueryAsync(async cancellationToken =>
        {
            using var call = _reflectionClient.ServerReflectionInfo(cancellationToken: cancellationToken);
            await call.RequestStream.WriteAsync(new ServerReflectionRequest { ListServices = "" }, cancellationToken)
                .ConfigureAwait(false);
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);

            var set = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (response.MessageResponseCase ==
                    ServerReflectionResponse.MessageResponseOneofCase.ListServicesResponse)
                {
                    foreach (var service in response.ListServicesResponse.Service)
                    {
                        set.Add(service.Name);
                    }
                }
            }

            return set;
        });
    }

    private Task<HashSet<string>?> QueryMethodsWithFallbackAsync(string serviceName)
    {
        return ExecuteReflectionQueryAsync(async cancellationToken =>
        {
            using var call = _reflectionClient.ServerReflectionInfo(cancellationToken: cancellationToken);
            await call.RequestStream.WriteAsync(
                    new ServerReflectionRequest { FileContainingSymbol = serviceName },
                    cancellationToken)
                .ConfigureAwait(false);
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);

            var set = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (response.MessageResponseCase !=
                    ServerReflectionResponse.MessageResponseOneofCase.FileDescriptorResponse)
                {
                    continue;
                }

                foreach (var raw in response.FileDescriptorResponse.FileDescriptorProto)
                {
                    var fd = FileDescriptorProto.Parser.ParseFrom(raw);
                    foreach (var svc in fd.Service)
                    {
                        var fqn = string.IsNullOrEmpty(fd.Package) ? svc.Name : $"{fd.Package}.{svc.Name}";
                        if (fqn != serviceName)
                        {
                            continue;
                        }

                        foreach (var m in svc.Method)
                        {
                            set.Add(m.Name);
                        }
                    }
                }
            }

            return set;
        });
    }

    private async Task<HashSet<string>?> ExecuteReflectionQueryAsync(
        Func<CancellationToken, Task<HashSet<string>>> query)
    {
        using var timeoutSource = new CancellationTokenSource(_reflectionTimeout);
        var queryTask = query(timeoutSource.Token);

        try
        {
            return await queryTask.WaitAsync(_reflectionTimeout, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (RpcException)
        {
            Volatile.Write(ref _reflectionUnavailable, 1);
            return null;
        }
        catch (OperationCanceledException)
        {
            Volatile.Write(ref _reflectionUnavailable, 1);
            return null;
        }
        catch (TimeoutException)
        {
            await timeoutSource.CancelAsync();
            ObserveFault(queryTask);
            Volatile.Write(ref _reflectionUnavailable, 1);
            return null;
        }
    }

    private static void ObserveFault(Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
