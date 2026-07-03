using Dapr.Messaging.PublishSubscribe;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Maps Dapr.Messaging dynamic subscription messages into actor stream events.
/// </summary>
public static class ActorStreamTopicMessageMapper
{
    /// <summary>
    /// Converts a Dapr.Messaging topic message to an actor stream event.
    /// </summary>
    public static ActorStreamEvent ToActorStreamEvent(TopicMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = message.Id,
            ["source"] = message.Source,
            ["type"] = message.Type,
            ["specversion"] = message.SpecVersion,
            ["datacontenttype"] = message.DataContentType,
            ["topic"] = message.Topic,
            ["pubsubname"] = message.PubSubName,
        };

        if (!string.IsNullOrWhiteSpace(message.Path))
        {
            attributes["path"] = message.Path;
        }

        foreach (var (key, value) in message.Extensions)
        {
            attributes[key] = ConvertValue(value);
        }

        return new ActorStreamEvent(message.Id, message.PubSubName, message.Topic, message.Data, attributes);
    }

    private static string ConvertValue(Value value) =>
        value.KindCase switch
        {
            Value.KindOneofCase.StringValue => value.StringValue,
            Value.KindOneofCase.NumberValue => value.NumberValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Value.KindOneofCase.BoolValue => value.BoolValue.ToString(),
            Value.KindOneofCase.NullValue => string.Empty,
            _ => value.ToString(),
        };
}
