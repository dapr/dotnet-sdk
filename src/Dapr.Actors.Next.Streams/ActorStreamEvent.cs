namespace Dapr.Actors.Next.Streams;

/// <summary>
/// CloudEvents-shaped event received from a streaming pub/sub subscription.
/// </summary>
public sealed record ActorStreamEvent(
    string Id,
    string PubsubName,
    string Topic,
    ReadOnlyMemory<byte> Data,
    IReadOnlyDictionary<string, string> Attributes)
{
    /// <summary>
    /// Gets the W3C traceparent attribute when present.
    /// </summary>
    public string? TraceParent => TryGetAttribute("traceparent", out var value) ? value : null;

    /// <summary>
    /// Tries to read a CloudEvents attribute by ordinal or case-insensitive key.
    /// </summary>
    public bool TryGetAttribute(string name, out string value)
    {
        if (Attributes.TryGetValue(name, out value!))
        {
            return true;
        }

        foreach (var item in Attributes)
        {
            if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
