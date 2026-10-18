// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Messaging.PublishSubscribe;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using P = Dapr.AppCallback.Autogen.Grpc.v1;

namespace Dapr.Messaging.Subscribe.AppCallback;

/// <summary>
/// Implements the Dapr runtime's <c>AppCallback</c> gRPC service so the sidecar can push
/// pub/sub events to the application. Routes <c>ListTopicSubscriptions</c> and
/// <c>OnTopicEvent</c> calls to the source-generated subscriber registry and dispatchers.
/// </summary>
/// <remarks>
/// Composability: this class is <c>partial</c> so a consumer that already implements
/// <c>AppCallback.AppCallbackBase</c> can add overrides in their own partial and call the base
/// implementation here for the pub/sub surface.
/// </remarks>
internal sealed partial class DaprAppCallbackService : P.AppCallback.AppCallbackBase
{
    private readonly IDaprMessagingSubscriberRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DaprAppCallbackService> _logger;

    public DaprAppCallbackService(
        IDaprMessagingSubscriberRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<DaprAppCallbackService> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Returns the subscriptions the runtime should push to this app. Built at request time from
    /// the source-generated registry, filtered to <see cref="DeliveryMode.Programmatic"/>.
    /// </summary>
    public override Task<P.ListTopicSubscriptionsResponse> ListTopicSubscriptions(
        Google.Protobuf.WellKnownTypes.Empty request,
        ServerCallContext context)
    {
        var response = new P.ListTopicSubscriptionsResponse();
        foreach (var descriptor in _registry.Descriptors)
        {
            if (descriptor.Delivery != DeliveryMode.Programmatic)
            {
                continue;
            }

            var sub = new P.TopicSubscription
            {
                PubsubName = descriptor.PubsubName,
                Topic = descriptor.TopicName,
            };

            if (!string.IsNullOrEmpty(descriptor.DeadLetterTopic))
            {
                sub.DeadLetterTopic = descriptor.DeadLetterTopic!;
            }

            foreach (var kvp in descriptor.Metadata)
            {
                sub.Metadata.Add(kvp.Key, kvp.Value);
            }

            response.Subscriptions.Add(sub);
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// Handles a single pushed topic event by dispatching it to the matching
    /// <see cref="ITopicDispatcher"/> and translating the returned
    /// <see cref="TopicResponseAction"/> to the gRPC <c>TopicEventResponseStatus</c>.
    /// </summary>
    public override async Task<P.TopicEventResponse> OnTopicEvent(
        P.TopicEventRequest request,
        ServerCallContext context)
    {
        var dispatcher = _registry.Resolve(request.PubsubName, request.Topic, DeliveryMode.Programmatic);
        if (dispatcher is null)
        {
            // No handler for this (pubsub, topic) under Programmatic delivery; drop to avoid an
            // endless retry loop on the sidecar.
            return new P.TopicEventResponse { Status = P.TopicEventResponse.Types.TopicEventResponseStatus.Drop };
        }

        var ctx = BuildContext(request);
        using var scope = _serviceProvider.CreateScope();
        var action = await dispatcher.DispatchAsync(request.Data.ToByteArray(), ctx, scope.ServiceProvider, context.CancellationToken);
        return new P.TopicEventResponse { Status = ToGrpc(action) };
    }

    /// <summary>
    /// Bulk variant (alpha). Aggregates per-entry responses; if no bulk dispatcher is available,
    /// each entry is dispatched individually via <see cref="OnTopicEvent"/>-style routing.
    /// </summary>
    public override async Task<P.TopicEventBulkResponse> OnBulkTopicEvent(
        P.TopicEventBulkRequest request,
        ServerCallContext context)
    {
        var response = new P.TopicEventBulkResponse();
        foreach (var entry in request.Entries)
        {
            var dispatcher = _registry.Resolve(request.PubsubName, request.Topic, DeliveryMode.Programmatic);
            TopicResponseAction action = TopicResponseAction.Drop;
            if (dispatcher is not null)
            {
                var ctx = new TopicContext
                {
                    PubsubName = request.PubsubName,
                    TopicName = request.Topic,
                    MessageId = entry.EntryId,
                    RawPayload = entry.Bytes.ToByteArray(),
                };
                using var scope = _serviceProvider.CreateScope();
                action = await dispatcher.DispatchAsync(entry.Bytes.ToByteArray(), ctx, scope.ServiceProvider, context.CancellationToken);
            }

            response.Statuses.Add(new P.TopicEventBulkResponseEntry
            {
                EntryId = entry.EntryId,
                Status = ToGrpc(action)
            });
        }

        return response;
    }

    private static TopicContext BuildContext(P.TopicEventRequest request)
    {
        var headers = new Dictionary<string, string>
        {
            ["id"] = request.Id,
            ["source"] = request.Source,
            ["type"] = request.Type,
            ["specversion"] = request.SpecVersion,
            ["datacontenttype"] = request.DataContentType,
            ["topic"] = request.Topic,
            ["pubsubname"] = request.PubsubName,
        };
        if (request.Path.Length > 0)
        {
            headers["path"] = request.Path;
        }

        return new TopicContext
        {
            PubsubName = request.PubsubName,
            TopicName = request.Topic,
            MessageId = request.Id,
            Headers = headers,
            RawPayload = request.Data.ToByteArray(),
        };
    }

    private static P.TopicEventResponse.Types.TopicEventResponseStatus ToGrpc(TopicResponseAction action) => action switch
    {
        TopicResponseAction.Success => P.TopicEventResponse.Types.TopicEventResponseStatus.Success,
        TopicResponseAction.Retry => P.TopicEventResponse.Types.TopicEventResponseStatus.Retry,
        _ => P.TopicEventResponse.Types.TopicEventResponseStatus.Drop,
    };
}
