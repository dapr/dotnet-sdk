// ------------------------------------------------------------------------
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

using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using Grpc.Net.Client;

namespace Dapr.Client.Test;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client.Autogen.Grpc.v1;
using Shouldly;
using Grpc.Core;
using Moq;
using Xunit;

public class PublishEventApiTest
{
    const string TestPubsubName = "testpubsubname";

    [Fact]
    public async Task PublishEventAsync_CanPublishTopicWithData()
    {
        await using var client = TestClient.CreateForDaprClient();

        var publishData = new PublishData() { PublishObjectParameter = "testparam" };
        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishEventAsync<PublishData>(TestPubsubName, "test", publishData);
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
        var jsonFromRequest = envelope.Data.ToStringUtf8();

        envelope.DataContentType.ShouldBe("application/json");
        envelope.PubsubName.ShouldBe(TestPubsubName);
        envelope.Topic.ShouldBe("test");
        jsonFromRequest.ShouldBe(JsonSerializer.Serialize(publishData, client.InnerClient.JsonSerializerOptions));
        envelope.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public async Task PublishEvent_ShouldRespectJsonStringEnumConverter()
    {
        //The following mimics how the TestClient is built, but adds the JsonStringEnumConverter to the serialization options
        var handler = new TestClient.CapturingHandler();
        var httpClient = new HttpClient(handler);
        var clientBuilder = new DaprClientBuilder()
            .UseJsonSerializationOptions(new JsonSerializerOptions()
            {
                Converters = {new JsonStringEnumConverter(null, false)}
            })
            .UseHttpClientFactory(() => httpClient)
            .UseGrpcChannelOptions(new GrpcChannelOptions()
            {
                HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true
            });
        var client = new TestClient<DaprClient>(clientBuilder.Build(), handler);
            
        //Ensure that the JsonStringEnumConverter is registered
        client.InnerClient.JsonSerializerOptions.Converters.Count.ShouldBe(1);
        client.InnerClient.JsonSerializerOptions.Converters.First().GetType().Name.ShouldMatch(nameof(JsonStringEnumConverter));

        var publishData = new Widget {Size = "Large", Color = WidgetColor.Red};
        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishEventAsync<Widget>(TestPubsubName, "test", publishData);
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
        var jsonFromRequest = envelope.Data.ToStringUtf8();
        jsonFromRequest.ShouldBe(JsonSerializer.Serialize(publishData, client.InnerClient.JsonSerializerOptions));
        jsonFromRequest.ShouldMatch("{\"Size\":\"Large\",\"Color\":\"Red\"}");
    }

    [Fact]
    public async Task PublishEventAsync_CanPublishTopicWithData_WithMetadata()
    {
        await using var client = TestClient.CreateForDaprClient();

        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        var publishData = new PublishData() { PublishObjectParameter = "testparam" };
        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishEventAsync<PublishData>(TestPubsubName, "test", publishData, metadata);
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
        var jsonFromRequest = envelope.Data.ToStringUtf8();

        envelope.DataContentType.ShouldBe("application/json");
        envelope.PubsubName.ShouldBe(TestPubsubName);
        envelope.Topic.ShouldBe("test");
        jsonFromRequest.ShouldBe(JsonSerializer.Serialize(publishData, client.InnerClient.JsonSerializerOptions));

        envelope.Metadata.Count.ShouldBe(2);
        envelope.Metadata.Keys.Contains("key1").ShouldBeTrue();
        envelope.Metadata.Keys.Contains("key2").ShouldBeTrue();
        envelope.Metadata["key1"].ShouldBe("value1");
        envelope.Metadata["key2"].ShouldBe("value2");
    }

    [Fact]
    public async Task PublishEventAsync_CanPublishTopicWithNoContent()
    {
        await using var client = TestClient.CreateForDaprClient();

        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishEventAsync(TestPubsubName, "test");
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();

        envelope.PubsubName.ShouldBe(TestPubsubName);
        envelope.Topic.ShouldBe("test");
        envelope.Data.Length.ShouldBe(0);
        envelope.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public async Task PublishEventAsync_CanPublishTopicWithNoContent_WithMetadata()
    {
        await using var client = TestClient.CreateForDaprClient();

        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishEventAsync(TestPubsubName, "test", metadata);
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
        envelope.PubsubName.ShouldBe(TestPubsubName);
        envelope.Topic.ShouldBe("test");
        envelope.Data.Length.ShouldBe(0);

        envelope.Metadata.Count.ShouldBe(2);
        envelope.Metadata.Keys.Contains("key1").ShouldBeTrue();
        envelope.Metadata.Keys.Contains("key2").ShouldBeTrue();
        envelope.Metadata["key1"].ShouldBe("value1");
        envelope.Metadata["key2"].ShouldBe("value2");
    }

    [Fact]
    public async Task PublishEventAsync_CanPublishCloudEventWithData()
    {
        await using var client = TestClient.CreateForDaprClient();

        var publishData = new PublishData() { PublishObjectParameter = "testparam" };
        var cloudEvent = new CloudEvent<PublishData>(publishData)
        {
            Source = new Uri("urn:testsource"),
            Type = "testtype",
            Subject = "testsubject",
        };
        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishEventAsync<CloudEvent<PublishData>>(TestPubsubName, "test", cloudEvent);
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
        var jsonFromRequest = envelope.Data.ToStringUtf8();

        envelope.DataContentType.ShouldBe("application/cloudevents+json");
        envelope.PubsubName.ShouldBe(TestPubsubName);
        envelope.Topic.ShouldBe("test");
        jsonFromRequest.ShouldBe(JsonSerializer.Serialize(cloudEvent, client.InnerClient.JsonSerializerOptions));
        envelope.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public async Task PublishEventAsync_CanPublishCloudEventWithNoContent()
    {
        await using var client = TestClient.CreateForDaprClient();

        var cloudEvent = new CloudEvent
        {
            Source = new Uri("urn:testsource"),
            Type = "testtype",
            Subject = "testsubject",
        };
        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishEventAsync<CloudEvent>(TestPubsubName, "test", cloudEvent);
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
        var jsonFromRequest = envelope.Data.ToStringUtf8();

        envelope.DataContentType.ShouldBe("application/cloudevents+json");
        envelope.PubsubName.ShouldBe(TestPubsubName);
        envelope.Topic.ShouldBe("test");
        jsonFromRequest.ShouldBe(JsonSerializer.Serialize(cloudEvent, client.InnerClient.JsonSerializerOptions));
        envelope.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public async Task PublishEventAsync_WithCancelledToken()
    {
        await using var client = TestClient.CreateForDaprClient();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await client.InnerClient.PublishEventAsync(TestPubsubName, "test", cancellationToken: cts.Token);
        });
    }

    // All overloads call through a common path that does exception handling.
    [Fact]
    public async Task PublishEventAsync_WrapsRpcException()
    {
        var client = new MockClient();

        var rpcStatus = new Status(StatusCode.Internal, "not gonna work");
        var rpcException = new RpcException(rpcStatus, new Metadata(), "not gonna work");

        // Setup the mock client to throw an Rpc Exception with the expected details info
        client.Mock
            .Setup(m => m.PublishEventAsync(It.IsAny<Autogen.Grpc.v1.PublishEventRequest>(), It.IsAny<CallOptions>()))
            .Throws(rpcException);

        var ex = await Assert.ThrowsAsync<DaprException>(async () =>
        {
            await client.DaprClient.PublishEventAsync("test", "test");
        });
        Assert.Same(rpcException, ex.InnerException);
    }

    [Fact]
    public async Task PublishEventAsync_CanPublishWithRawData()
    {
        await using var client = TestClient.CreateForDaprClient();

        var publishData = new PublishData() { PublishObjectParameter = "testparam" };
        var publishBytes = JsonSerializer.SerializeToUtf8Bytes(publishData);
        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.PublishByteEventAsync(TestPubsubName, "test", publishBytes.AsMemory());
        });

        request.Dismiss();

        var envelope = await request.GetRequestEnvelopeAsync<PublishEventRequest>();
        var jsonFromRequest = envelope.Data.ToStringUtf8();

        envelope.DataContentType.ShouldBe("application/json");
        envelope.PubsubName.ShouldBe(TestPubsubName);
        envelope.Topic.ShouldBe("test");
        jsonFromRequest.ShouldBe(JsonSerializer.Serialize(publishData));
        // The default serializer forces camel case, so this should be different from our serialization above.
        jsonFromRequest.ShouldNotBe(JsonSerializer.Serialize(publishBytes, client.InnerClient.JsonSerializerOptions));
        envelope.Metadata.Count.ShouldBe(0);
    }

    private class PublishData
    {
        public string PublishObjectParameter { get; set; }
    }

    private class Widget
    {
        public string Size { get; set; }
        public WidgetColor Color { get; set; }
    }

    private enum WidgetColor
    {
        Red,
        Green,
        Yellow
    }
}