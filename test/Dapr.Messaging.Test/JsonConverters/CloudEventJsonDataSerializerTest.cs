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

namespace Dapr.Messaging.Test.JsonConverters;

public class CloudEventJsonDataSerializerTest
{
    private readonly CloudEventDataJsonSerializer<string> _converter = new();

    [Fact]
    public void Write_ShouldWriteCloudEventWithData_WhenDataContentTypeIsJson()
    {
        var cloudEvent = new CloudEvent<string>(
            new Uri("https://example.com/source"),
            "example.type",
            "example data")
        {
            Subject = "example subject",
            Time = DateTimeOffset.Parse("2025-04-13T06:35:22.000Z"),
            DataContentType = "application/json"
        };

        var options = new JsonSerializerOptions();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        _converter.Write(writer, cloudEvent, options);
        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        const string expectedJson = "{\"source\":\"https://example.com/source\",\"type\":\"example.type\",\"specversion\":\"1.0\",\"subject\":\"example subject\",\"time\":\"2025-04-13T06:35:22.000Z\",\"Data\":\"example data\"}";
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void Write_ShouldWriteCloudEventWithData_WhenDataContentTypeIsNotJson()
    {
        var cloudEvent = new CloudEvent<string>(
            new Uri("https://example.com/source"),
            "example.type",
            "example data")
        {
            Subject = "example subject",
            Time = DateTimeOffset.Parse("2025-04-13T06:35:22.000Z"),
            DataContentType = "text/plain"
        };

        var options = new JsonSerializerOptions();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        _converter.Write(writer, cloudEvent, options);
        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        const string expectedJson = "{\"source\":\"https://example.com/source\",\"type\":\"example.type\",\"specversion\":\"1.0\",\"subject\":\"example subject\",\"time\":\"2025-04-13T06:35:22.000Z\",\"Data\":\"example data\"}";
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void Write_ShouldWriteCloudEventWithoutOptionalFields()
    {
        var cloudEvent = new CloudEvent<string>(
            new Uri("https://example.com/source"),
            "example.type",
            "example data");

        var options = new JsonSerializerOptions();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        _converter.Write(writer, cloudEvent, options);
        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        const string expectedJson = "{\"source\":\"https://example.com/source\",\"type\":\"example.type\",\"specversion\":\"1.0\",\"Data\":\"example data\"}";
        Assert.Equal(expectedJson, json);
    }
}
