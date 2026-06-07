using Dapr.Metadata.Abstractions;

namespace Dapr.Metadata.Runtime;

/// <summary>
/// Represents a provider for retrieving metadata from the Dapr runtime.
/// </summary>
internal interface IDaprMetadataProvider
{
    /// <summary>
    /// Retrieves the metadata value from the Dapr runtime.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An instance of a <see cref="DaprMetadata"/>.</returns>
    ValueTask<DaprMetadata> GetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Used to force an on-demand data refresh.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An instance of a <see cref="DaprMetadata"/>.</returns>
    ValueTask<DaprMetadata> RefreshAsync(CancellationToken cancellationToken = default);
}
