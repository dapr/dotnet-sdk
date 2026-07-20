using Microsoft.Extensions.Primitives;

namespace Dapr.Metadata.Runtime;

internal sealed class DaprMetadataRefreshSignal
{
    private CancellationTokenSource _cts = new();

    public IChangeToken GetChangeToken() => new CancellationChangeToken(_cts.Token);
    
    public void NotifyChanged()
    {
        var previous = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        previous.Cancel(); // Fires the monitor's listeners; they recompute via the factory
        previous.Dispose();
    }
}
