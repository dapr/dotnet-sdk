namespace Dapr.Jobs;

/// <summary>
/// Options used to configure the Dapr job client.
/// </summary>
public class DaprJobClientOptions
{
    /// <summary>
    /// Initializes a new instance of <see cref="DaprJobClientOptions"/>.
    /// </summary>
    /// <param name="appId">The ID of the app .</param>
    /// <param name="appNamespace">The namespace of the app.</param>
    public DaprJobClientOptions(string appId, string appNamespace)
    {
        AppId = appId;
        Namespace = appNamespace;
    }

    /// <summary>
    /// The App ID of the requester.
    /// </summary>
    public string AppId { get; init; }

    /// <summary>
    /// The namespace of the requester.
    /// </summary>
    public string Namespace { get; init; }
}
