// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// ------------------------------------------------------------------------

using System.Text.Json;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Messaging.Subscribe.Http;

internal static class DaprHttpSubscriptionEndpoint
{
    public static async Task HandleDiscoveryAsync(HttpContext context)
    {
        var registry = context.RequestServices.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        var subscriptions = registry.Descriptors
            .Where(d => d.Delivery == DeliveryMode.Http)
            .Select(d => new
            {
                pubsubname = d.PubsubName,
                topic = d.TopicName,
                route = d.Route.TrimStart('/'),
                metadata = BuildMetadata(d),
                deadLetterTopic = d.DeadLetterTopic,
                routes = string.IsNullOrWhiteSpace(d.Match)
                    ? null
                    : new
                    {
                        rules = new[] { new { match = d.Match, path = d.Route.TrimStart('/') } },
                        @default = d.Route.TrimStart('/')
                    },
                bulkSubscribe = d.BulkSubscribe?.Enabled == true
                    ? new
                    {
                        enabled = true,
                        maxMessagesCount = d.BulkSubscribe.MaxMessagesCount,
                        maxAwaitDurationMs = d.BulkSubscribe.MaxAwaitDurationMs
                    }
                    : null
            });

        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(context.Response.Body, subscriptions, cancellationToken: context.RequestAborted);
    }

    public static async Task HandleEventAsync(HttpContext context, TopicSubscriptionDescriptor descriptor)
    {
        var registry = context.RequestServices.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        var dispatcher = registry.Resolve(descriptor.PubsubName, descriptor.TopicName, DeliveryMode.Http);
        if (dispatcher is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        if (descriptor.BulkSubscribe?.Enabled == true &&
            document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("entries", out var entries) &&
            entries.ValueKind == JsonValueKind.Array)
        {
            var statuses = new List<object>();
            foreach (var entry in entries.EnumerateArray())
            {
                var result = await DispatchAsync(entry, dispatcher, descriptor, context);
                statuses.Add(new { entryId = GetString(entry, "entryId"), status = ToStatus(result) });
            }

            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new { statuses },
                cancellationToken: context.RequestAborted);
            return;
        }

        var singleResult = await DispatchAsync(document.RootElement, dispatcher, descriptor, context);

        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new { status = ToStatus(singleResult) },
            cancellationToken: context.RequestAborted);
    }

    private static async Task<TopicResponseAction> DispatchAsync(
        JsonElement envelope,
        ITopicDispatcher dispatcher,
        TopicSubscriptionDescriptor descriptor,
        HttpContext context)
    {
        var payload = ExtractPayload(envelope, out var headers);
        var messageId = GetString(envelope, "id") ?? GetString(envelope, "entryId") ?? Guid.NewGuid().ToString("N");
        var topic = GetString(envelope, "topic") ?? descriptor.TopicName;
        var pubsub = GetString(envelope, "pubsubname") ?? descriptor.PubsubName;
        var topicContext = new TopicContext
        {
            PubsubName = pubsub,
            TopicName = topic,
            MessageId = messageId,
            Headers = headers,
            RawPayload = payload
        };

        using var scope = context.RequestServices.CreateScope();
        return await dispatcher.DispatchAsync(payload, topicContext, scope.ServiceProvider, context.RequestAborted);
    }

    private static byte[] ExtractPayload(JsonElement envelope, out IReadOnlyDictionary<string, string> headers)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "id", "source", "type", "specversion", "datacontenttype", "topic", "pubsubname" })
        {
            var value = GetString(envelope, name);
            if (value is not null)
            {
                values[name] = value;
            }
        }

        if (envelope.TryGetProperty("data_base64", out var base64) ||
            envelope.TryGetProperty("bytes", out base64))
        {
            headers = values;
            return Convert.FromBase64String(base64.GetString() ?? string.Empty);
        }

        if (envelope.TryGetProperty("data", out var data) ||
            envelope.TryGetProperty("event", out data))
        {
            headers = values;
            return JsonSerializer.SerializeToUtf8Bytes(data);
        }

        headers = values;
        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyDictionary<string, string>? BuildMetadata(TopicSubscriptionDescriptor descriptor)
    {
        if (descriptor.Metadata.Count == 0 && descriptor.EnableRawPayload != true)
        {
            return null;
        }

        var metadata = new Dictionary<string, string>(descriptor.Metadata);
        if (descriptor.EnableRawPayload == true)
        {
            metadata["rawPayload"] = "true";
        }

        return metadata;
    }

    private static string ToStatus(TopicResponseAction action) => action switch
    {
        TopicResponseAction.Success => "SUCCESS",
        TopicResponseAction.Retry => "RETRY",
        _ => "DROP"
    };
}
