using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Response for a Subscribe Configuration request.
/// </summary>
/// <param name="source">The <see cref="ConfigurationSource"/> that provides the actual data from the subscription.</param>
public class SubscribeConfigurationResponse(ConfigurationSource source)
{
    /// <summary>
    /// The Id of the Subscription. This will be <see cref="string.Empty"/> until the first result has been streamed.
    /// After that time, the Id can be used to unsubscribe.
    /// </summary>
    public string Id => source.Id;

    /// <summary>
    /// Get the <see cref="ConfigurationSource"/> that is used to read the actual subscribed configuration data.
    /// </summary>
    public IAsyncEnumerable<IDictionary<string, ConfigurationItem>> Source => source;
}
