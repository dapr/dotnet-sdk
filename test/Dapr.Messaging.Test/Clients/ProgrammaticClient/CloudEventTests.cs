// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Text.Json;
using Dapr.Messaging.JsonConverters;
using Dapr.Messaging.PublishSubscribe;
using Shouldly;
using Uri = System.Uri;

namespace Dapr.Messaging.Test.Clients.ProgrammaticClient;

public class CloudEventTests
{
    [Fact]
    public void CloudEvent_ShouldHaveCorrectProperties()
    {
        var source = new Uri("https://example.com");
        const string type = "example.type";
        const string subject = "example.subject";
        var time = DateTimeOffset.UtcNow;

        var cloudEvent = new CloudEvent(source, type) { Subject = subject, Time = time };

        cloudEvent.Source.ShouldBe(source);
        cloudEvent.Type.ShouldBe(type);
        cloudEvent.Subject.ShouldBe(subject);
        cloudEvent.Time.ShouldBe(time);
        cloudEvent.SpecVersion.ShouldBe("1.0");
    }

    [Fact]
    public void CloudEvent_ShouldSerializeCorrectly()
    {
        var source = new Uri("https://example.com");
        const string type = "example.type";
        var cloudEvent = new CloudEvent(source, type);
        var json = JsonSerializer.Serialize(cloudEvent);
        
        json.ShouldContain("\"source\":\"https://example.com\"");
        json.ShouldContain($"\"type\":\"{type}\"");
        json.ShouldContain("\"specversion\":\"1.0\"");
    }

    [Fact]
    public void TypedCloudEvent_ShouldHaveCorrectProperties()
    {
        var source = new Uri("https://example.com");
        const string type = "example.type";
        var cloudEvent = new CloudEvent(source, type);

        var json = JsonSerializer.Serialize(cloudEvent);
        json.ShouldContain("\"source\":\"https://example.com\"");
        json.ShouldContain($"\"type\":\"{type}\"");
        json.ShouldContain("\"specversion\":\"1.0\"");
    }

    [Fact]
    public void TypedCloudEvent_ShouldSerializeCorrectly()
    {
        var source = new Uri("https://example.com");
        const string type = "example.type";
        var data = new { Key = "value" };
        var cloudEventWithData = new CloudEvent<object>(source, type, data);

        var options = new JsonSerializerOptions { Converters = { new CloudEventDataJsonSerializer<object>() } };
        var json = JsonSerializer.Serialize(cloudEventWithData, options);

        json.ShouldContain("\"source\":\"https://example.com/\"");
        json.ShouldContain($"\"type\":\"{type}\"");
        json.ShouldContain("\"data\":{\"Key\":\"value\"}");
        json.ShouldContain("\"specversion\":\"1.0\"");
    }
}
