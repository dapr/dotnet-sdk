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

using System.Text.Json;
using Dapr.Metadata.Abstractions;

namespace Dapr.Metadata.Test;

public sealed class DaprMetadataJsonTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Constructor_InitializesCollectionsToEmpty()
    {
        var metadata = new DaprMetadata();

        Assert.Empty(metadata.EnabledFeatures);
        Assert.Empty(metadata.Actors);
        Assert.Empty(metadata.CustomAttributes);
        Assert.Empty(metadata.Components);
        Assert.Empty(metadata.HttpEndpoints);
        Assert.Empty(metadata.Subscriptions);
        Assert.NotNull(metadata.AppConnectionProperties);
        Assert.NotNull(metadata.SchedulerMetadata);
        Assert.NotNull(metadata.Workflows);
    }

    [Fact]
    public void Deserialize_UsesDaprMetadataJsonNames()
    {
        const string json = """
            {
              "id": "orders",
              "runtimeVersion": "1.15.0",
              "enabledFeatures": [ "feature-a" ],
              "actors": [ { "type": "CartActor", "count": 2 } ],
              "extended": { "region": "central" },
              "components": [ { "name": "statestore", "type": "state.redis", "version": "v1", "capabilities": [ "ETAG" ] } ],
              "httpEndpoints": [ { "name": "downstream" } ],
              "subscriptions": [
                {
                  "pubsubname": "pubsub",
                  "topic": "orders",
                  "metadata": { "rawPayload": "true" },
                  "rules": [ { "match": "event.type == 'created'", "path": "/orders" } ],
                  "deadLetterTopic": "orders-dead",
                  "type": "STREAMING"
                }
              ],
              "appConnectionProperties":
                {
                  "port": 5000,
                  "protocol": "http",
                  "channelAddress": "127.0.0.1",
                  "maxConcurrency": 16,
                  "health": {
                    "healthCheckPath": "/healthz",
                    "healthProbeInterval": "5s",
                    "healthProbeTimeout": "1s",
                    "healthThreshold": 3
                  }
                },
              "scheduler": { "connected_addresses": [ "localhost:50006" ] },
              "workflows": { "connectedWorkers": 4 }
            }
            """;

        var metadata = JsonSerializer.Deserialize<DaprMetadata>(json, SerializerOptions);

        Assert.NotNull(metadata);
        Assert.Equal("orders", metadata.AppId);
        Assert.Equal("1.15.0", metadata.RuntimeVersion);
        Assert.Equal("feature-a", Assert.Single(metadata.EnabledFeatures));
        Assert.Equal("central", metadata.CustomAttributes["region"]);

        var actor = Assert.Single(metadata.Actors);
        Assert.Equal("CartActor", actor.Type);
        Assert.Equal(2, actor.Count);

        var component = Assert.Single(metadata.Components);
        Assert.Equal("statestore", component.Name);
        Assert.Equal("state.redis", component.Type);
        Assert.Equal("v1", component.Version);
        Assert.Equal("ETAG", Assert.Single(component.Capabilities));

        Assert.Equal("downstream", Assert.Single(metadata.HttpEndpoints).Name);

        var subscription = Assert.Single(metadata.Subscriptions);
        Assert.Equal("pubsub", subscription.PubSubName);
        Assert.Equal("orders", subscription.Topic);
        Assert.IsType<JsonElement>(subscription.Metadata);
        Assert.Equal("orders-dead", subscription.DeadLetterTopic);
        Assert.Equal(SubscriptionType.Streaming, subscription.Type);
        var rule = Assert.Single(subscription.Rules);
        Assert.Equal("event.type == 'created'", rule.Match);
        Assert.Equal("/orders", rule.Path);

        var appConnection = metadata.AppConnectionProperties;
        Assert.Equal(5000, appConnection.Port);
        Assert.Equal("http", appConnection.Protocol);
        Assert.Equal("127.0.0.1", appConnection.ChannelAddress);
        Assert.Equal(16, appConnection.MaxConcurrency);
        Assert.NotNull(appConnection.Health);
        Assert.Equal("/healthz", appConnection.Health.HealthCheckPath);
        Assert.Equal("5s", appConnection.Health.HealthProbeInterval);
        Assert.Equal("1s", appConnection.Health.HealthProbeTimeout);
        Assert.Equal(3, appConnection.Health.HealthThreshold);

        Assert.Equal("localhost:50006", Assert.Single(metadata.SchedulerMetadata.ConnectedAddresses));
        Assert.Equal(4, metadata.Workflows.ConnectedWorkers);
    }

    [Theory]
    [InlineData("DECLARATIVE", SubscriptionType.Declarative)]
    [InlineData("STREAMING", SubscriptionType.Streaming)]
    [InlineData("PROGRAMMATIC", SubscriptionType.Programmatic)]
    public void Deserialize_SubscriptionType_UsesEnumMemberValues(string jsonValue, SubscriptionType expected)
    {
        var metadata = JsonSerializer.Deserialize<SubscriptionMetadata>($$"""{ "type": "{{jsonValue}}" }""", SerializerOptions);

        Assert.NotNull(metadata);
        Assert.Equal(expected, metadata.Type);
    }
}
