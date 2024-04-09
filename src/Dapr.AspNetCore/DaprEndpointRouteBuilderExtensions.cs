﻿// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Microsoft.AspNetCore.Builder
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Dapr;
    using Dapr.AspNetCore;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Patterns;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Contains extension methods for <see cref="IEndpointRouteBuilder" />.
    /// </summary>
    public static class DaprEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps an endpoint that will respond to requests to <c>/dapr/subscribe</c> from the
        /// Dapr runtime.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder" />.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder" />.</returns>
        public static IEndpointConventionBuilder MapSubscribeHandler(this IEndpointRouteBuilder endpoints)
        {
            return CreateSubscribeEndPoint(endpoints);
        }

        /// <summary>
        /// Maps an endpoint that will respond to requests to <c>/dapr/subscribe</c> from the
        /// Dapr runtime.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder" />.</param>
        /// <param name="options">Configuration options</param>
        /// <returns>The <see cref="IEndpointConventionBuilder" />.</returns>
        /// <seealso cref="MapSubscribeHandler(IEndpointRouteBuilder)"/>
        public static IEndpointConventionBuilder MapSubscribeHandler(this IEndpointRouteBuilder endpoints, SubscribeOptions? options)
        {
            return CreateSubscribeEndPoint(endpoints, options);
        }

        private static IEndpointConventionBuilder CreateSubscribeEndPoint(IEndpointRouteBuilder endpoints, SubscribeOptions? options = null)
        {
            if (endpoints is null)
            {
                throw new System.ArgumentNullException(nameof(endpoints));
            }

            return endpoints.MapGet("dapr/subscribe", async context =>
            {
                var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("DaprTopicSubscription");
                var dataSource = context.RequestServices.GetRequiredService<EndpointDataSource>();
                var subscriptions = dataSource.Endpoints
                    .OfType<RouteEndpoint>()
                    .Where(e => e.Metadata.GetOrderedMetadata<ITopicMetadata>().Any(t => t.Name != null)) // only endpoints which have TopicAttribute with not null Name.
                    .SelectMany(e =>
                    {
                        var topicMetadata = e.Metadata.GetOrderedMetadata<ITopicMetadata>();
                        var originalTopicMetadata = e.Metadata.GetOrderedMetadata<IOriginalTopicMetadata>();
                        var bulkSubscribeMetadata = e.Metadata.GetOrderedMetadata<IBulkSubscribeMetadata>();

                        var subs = new List<(string? PubsubName, string? Name, string? DeadLetterTopic, bool? EnableRawPayload, 
                            string? Match, int Priority, Dictionary<string, string[]>? OriginalTopicMetadata, 
                            string? MetadataSeparator, RoutePattern RoutePattern, DaprTopicBulkSubscribe bulkSubscribe)>();

                        for (int i = 0; i < topicMetadata.Count(); i++)
                        {
                            DaprTopicBulkSubscribe? bulkSubscribe = null;

                            foreach (var bulkSubscribeAttr in bulkSubscribeMetadata)
                            {
                                if (bulkSubscribeAttr.TopicName != topicMetadata[i].Name)
                                {
                                    continue;
                                }

                                bulkSubscribe = new DaprTopicBulkSubscribe
                                {
                                    Enabled = true,
                                    MaxMessagesCount = bulkSubscribeAttr.MaxMessagesCount,
                                    MaxAwaitDurationMs = bulkSubscribeAttr.MaxAwaitDurationMs
                                };
                                break;
                            }

                            // var pubsubName = topicMetadata[i].PubsubName;
                            // var name = topicMetadata[i].Name;
                            // var deadLetterTopic = (topicMetadata[i] as IDeadLetterTopicMetadata)?.DeadLetterTopic;
                            // var enableRawPayload = (topicMetadata[i] as IRawTopicMetadata)?.EnableRawPayload;
                            // var match = topicMetadata[i].Match;
                            // var priority = topicMetadata[i].Priority;
                            // var afterPriority = originalTopicMetadata.Where(m => (topicMetadata[i] as IOwnedOriginalTopicMetadata)?.OwnedMetadatas?.Any(o => o.Equals(m.Id)) == true || string.IsNullOrEmpty(m.Id))
                            //     .GroupBy(c => c.Name)
                            //     .ToDictionary(m => m.Key, m => m.Select(c => c.Value).Distinct().ToArray());
                            // var metadataSeparator = (topicMetadata[i] as IOwnedOriginalTopicMetadata)?.MetadataSeparator;
                            // var routePattern = e.RoutePattern;
                            // var bulkSubscribeValue = bulkSubscribe;
                            
                            
                            subs.Add((topicMetadata[i].PubsubName,
                                topicMetadata[i].Name,
                                (topicMetadata[i] as IDeadLetterTopicMetadata)?.DeadLetterTopic,
                                (topicMetadata[i] as IRawTopicMetadata)?.EnableRawPayload,
                                topicMetadata[i].Match,
                                topicMetadata[i].Priority,
                                originalTopicMetadata.Where(m => (topicMetadata[i] as IOwnedOriginalTopicMetadata)?.OwnedMetadatas?.Any(o => o.Equals(m.Id)) == true || string.IsNullOrEmpty(m.Id))
                                                     .GroupBy(c => c.Name)
                                                     .ToDictionary(m => m.Key, m => m.Select(c => c.Value).Distinct().ToArray()),
                                (topicMetadata[i] as IOwnedOriginalTopicMetadata)?.MetadataSeparator,
                                e.RoutePattern,
                                bulkSubscribe));
                        }

                        return subs;
                    })
                    .Distinct()
                    .GroupBy(e => new { e.PubsubName, e.Name })
                    .Select(e => e.OrderBy(e => e.Priority))
                    .Select(e =>
                    {
                        (string PubsubName, string Name, string? DeadLetterTopic, bool? EnableRawPayload, string? Match, int Priority, Dictionary<string, string[]> OriginalTopicMetadata, string? MetadataSeparator, RoutePattern RoutePattern, DaprTopicBulkSubscribe bulkSubscribe) first = e.First();
                        var rawPayload = e.Any(f => f.EnableRawPayload.GetValueOrDefault());
                        var separator = e.FirstOrDefault(f => !string.IsNullOrEmpty(f.MetadataSeparator));
                        var metadataSeparator = separator.MetadataSeparator ?? ",";
                        List<(string PubsubName, string Name, string DeadLetterTopic, bool? EnableRawPayload, string Match, int Priority, Dictionary<string, string[]> OriginalTopicMetadata, string MetadataSeparator, RoutePattern RoutePattern, DaprTopicBulkSubscribe bulkSubscribe)> rules = e.Where(f => !string.IsNullOrEmpty(f.Match)).ToList();
                        var defaultRoutes = e.Where(f => string.IsNullOrEmpty(f.Match)).Select(f => RoutePatternToString(f.RoutePattern)).ToList();
                        var defaultRoute = defaultRoutes.FirstOrDefault();

                        //multiple identical names. use comma separation.
                        var metadata = new Metadata(e.SelectMany(c => c.OriginalTopicMetadata).GroupBy(c => c.Key).ToDictionary(c => c.Key, c => string.Join(metadataSeparator, c.SelectMany(c => c.Value).Distinct())));
                        if (rawPayload || options?.EnableRawPayload is true)
                        {
                            metadata.Add(Metadata.RawPayload, "true");
                        }

                        if (logger != null)
                        {
                            if (defaultRoutes.Count > 1)
                            {
                                logger.LogError("A default subscription to topic {name} on pubsub {pubsub} already exists.", first.Name, first.PubsubName);
                            }

                            var duplicatePriorities = rules.GroupBy(e => e.Priority)
                              .Where(g => g.Count() > 1)
                              .ToDictionary(x => x.Key, y => y.Count());

                            foreach (var entry in duplicatePriorities)
                            {
                                logger.LogError("A subscription to topic {name} on pubsub {pubsub} has duplicate priorities for {priority}: found {count} occurrences.", first.Name, first.PubsubName, entry.Key, entry.Value);
                            }
                        }

                        var subscription = new Subscription
                        {
                            Topic = first.Name,
                            PubsubName = first.PubsubName,
                            Metadata = metadata.Count > 0 ? metadata : null,
                            BulkSubscribe = first.bulkSubscribe
                        };

                        if (first.DeadLetterTopic != null)
                        {
                            subscription.DeadLetterTopic = first.DeadLetterTopic;
                        }

                        // Use the V2 routing rules structure
                        if (rules.Count > 0)
                        {
                            subscription.Routes = new Routes
                            {
                                Rules = rules.Select(e => new Rule
                                {
                                    Match = e.Match,
                                    Path = RoutePatternToString(e.RoutePattern),
                                }).ToList(),
                                Default = defaultRoute,
                            };
                        }
                        // Use the V1 structure for backward compatibility.
                        else
                        {
                            subscription.Route = defaultRoute;
                        }

                        return subscription;
                    })
                    .OrderBy(e => (e.PubsubName, e.Topic));

                await context.Response.WriteAsync(JsonSerializer.Serialize(subscriptions,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    }));
            });
        }

        private static string RoutePatternToString(RoutePattern routePattern)
        {
            return string.Join("/", routePattern.PathSegments
                                    .Select(segment => string.Concat(segment.Parts.Cast<RoutePatternLiteralPart>()
                                    .Select(part => part.Content))));
        }
    }
}
