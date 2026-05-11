using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1;

namespace Dapr.Common;

/// <summary>
/// Used to determine Dapr runtime capability for fallback purposes by the SDKs.
/// </summary>
/// <param name="channel">The <see cref="GrpcChannel"/> to validate with.</param>
internal sealed class DaprRuntimeCapabilities(GrpcChannel channel) : IDaprRuntimeCapabilities, IDisposable
{
    private readonly ServerReflection.ServerReflectionClient _reflectionClient = new(channel);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HashSet<string>? _cachedServices = null;
    private readonly Dictionary<string, HashSet<string>> _cachedMethodsByService = [];
    
    /// <inheritdocs />
    public async Task<bool> SupportsMethodAsync(string fullyQualifiedMethodName, CancellationToken cancellationToken = default)
    {
        var slash = fullyQualifiedMethodName.LastIndexOf('/');
        if (slash <= 0)
        {
            throw new ArgumentException("Expected the form 'package.Service/Method.", nameof(fullyQualifiedMethodName));
        }

        var service = fullyQualifiedMethodName[..slash];
        var method = fullyQualifiedMethodName[(slash + 1)..];

        var methods = await GetMethodsForServiceAsync(service, cancellationToken).ConfigureAwait(false);
        return methods.Contains(method);
    }

    /// <inheritdocs />
    public async Task<bool> SupportsServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var services = await GetServicesAsync(cancellationToken).ConfigureAwait(false);
        return services.Contains(serviceName); 
    }

    private async Task<HashSet<string>> GetServicesAsync(CancellationToken cancellationToken)
    {
        if (_cachedServices is not null)
            return _cachedServices;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedServices is not null)
                return _cachedServices;

            using var call = _reflectionClient.ServerReflectionInfo(cancellationToken: cancellationToken);
            await call.RequestStream.WriteAsync(new ServerReflectionRequest { ListServices = "" }, cancellationToken)
                .ConfigureAwait(false);

            var set = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (response.MessageResponseCase ==
                    ServerReflectionResponse.MessageResponseOneofCase.ListServicesResponse)
                {
                    foreach (var s in response.ListServicesResponse.Service)
                    {
                        set.Add(s.Name);
                    }
                }
            }

            _cachedServices = set;
            return set;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<HashSet<string>> GetMethodsForServiceAsync(string serviceName,
        CancellationToken cancellationToken)
    {
        if (_cachedMethodsByService.TryGetValue(serviceName, out var existing))
            return existing;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedMethodsByService.TryGetValue(serviceName, out existing))
                return existing;

            using var call = _reflectionClient.ServerReflectionInfo(cancellationToken: cancellationToken);
            await call.RequestStream.WriteAsync(new ServerReflectionRequest { FileContainingSymbol = serviceName }, cancellationToken)
                .ConfigureAwait(false);
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);

            var set = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (response.MessageResponseCase !=
                    ServerReflectionResponse.MessageResponseOneofCase.FileDescriptorResponse)
                    continue;

                foreach (var raw in response.FileDescriptorResponse.FileDescriptorProto)
                {
                    var fd = FileDescriptorProto.Parser.ParseFrom(raw);
                    foreach (var svc in fd.Service)
                    {
                        var fqn = string.IsNullOrEmpty(fd.Package) ? svc.Name : $"{fd.Package}.{svc.Name}";
                        if (fqn != serviceName)
                            continue;
                        foreach (var m in svc.Method)
                        {
                            set.Add(m.Name);
                        }
                    }
                }
            }

            _cachedMethodsByService[serviceName] = set;
            return set;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
