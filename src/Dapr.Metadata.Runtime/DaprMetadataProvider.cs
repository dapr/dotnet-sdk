using System.Net.Http.Json;
using Dapr.Metadata.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Dapr.Metadata.Runtime;

internal sealed class DaprMetadataProvider(IHttpClientFactory httpClientFactory, DaprMetadataRefreshSignal signal, IConfiguration? configuration = null) : IDaprMetadataProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DaprMetadata? _cached;
    private readonly HttpClient _client = DaprDefaults.CreateDefaultHttpClient(httpClientFactory, configuration);
    
    public async ValueTask<DaprMetadata> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is { } cachedValue) return cachedValue; // Fastest path, no lock

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } existing) return existing; // double-check
            return await FetchLockedAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<DaprMetadata> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await FetchLockedAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    private async Task<DaprMetadata> FetchLockedAsync(CancellationToken cancellationToken = default)
    {
        using var resp = await _client.GetAsync("/v1.0/metadata", cancellationToken);
        resp.EnsureSuccessStatusCode();
        
        var data = await resp.Content.ReadFromJsonAsync<DaprMetadata>(cancellationToken) ?? throw
            new InvalidOperationException("Empty Datpr metadata response");

        _cached = data;
        signal.NotifyChanged();
        return data;
    }
}
