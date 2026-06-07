using Dapr.Metadata.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Dapr.Metadata.Runtime;

internal sealed class DaprMetadataChangeTokenSource(DaprMetadataRefreshSignal signal) : IOptionsChangeTokenSource<DaprMetadata>
{
    public IChangeToken GetChangeToken() => signal.GetChangeToken();

    public string? Name => Options.DefaultName;
}
