namespace Dapr.Client;

/// <summary>
/// Response from an Unsubscribe Configuration call.
/// </summary>
/// <param name="ok">Boolean indicating success.</param>
/// <param name="message">Message from the Configuration API.</param>
public class UnsubscribeConfigurationResponse(bool ok, string message)
{
    /// <summary>
    /// Boolean representing if the request was successful or not.
    /// </summary>
    public bool Ok { get; } = ok;

    /// <summary>
    /// The message from the Configuration API.
    /// </summary>
    public string Message { get; } = message;
}
