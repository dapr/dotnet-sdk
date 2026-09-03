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

namespace Dapr.Client.Test;

using System;
using System.Text.Json;
using Shouldly;
using Xunit;

public class CloudEventTest
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private class OrderData
    {
        public int OrderId { get; set; }
    }

    [Fact]
    public void Serialize_AllFieldsSet_EmitsLowercaseConstantKeys()
    {
        var ce = new CloudEvent<OrderData>(new OrderData { OrderId = 1 })
        {
            Id = "5929aaac-a5e2-4ca1-859c-edfe73f11565",
            Source = new Uri("urn:checkout"),
            SpecVersion = "1.0",
            Type = "com.dapr.event.sent",
            Time = new DateTimeOffset(2020, 9, 23, 6, 23, 21, TimeSpan.Zero),
            Subject = "order/42",
            TraceId = "00-113ad9c4e42b27583ae98ba698d54255-e3743e35ff56f219-01",
            TraceParent = "00-113ad9c4e42b27583ae98ba698d54255-e3743e35ff56f219-01",
            TraceState = "key=value",
        };

        var json = JsonSerializer.Serialize(ce, WebOptions);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Each JSON key must match the shared constant (single source of truth) and carry
        // the value set on the instance.
        AssertString(root, CloudEventPropertyNames.Id, ce.Id);
        AssertString(root, CloudEventPropertyNames.Source, ce.Source.ToString());
        AssertString(root, CloudEventPropertyNames.SpecVersion, ce.SpecVersion);
        AssertString(root, CloudEventPropertyNames.Type, ce.Type);
        AssertString(root, CloudEventPropertyNames.Subject, ce.Subject);
        AssertString(root, CloudEventPropertyNames.TraceId, ce.TraceId);
        AssertString(root, CloudEventPropertyNames.TraceParent, ce.TraceParent);
        AssertString(root, CloudEventPropertyNames.TraceState, ce.TraceState);
        AssertString(root, CloudEventPropertyNames.DataContentType, ce.DataContentType);

        // 'time' and 'data' are present; time is ISO-8601, data carries the typed payload.
        root.TryGetProperty(CloudEventPropertyNames.Time, out _).ShouldBeTrue();
        root.TryGetProperty(CloudEventPropertyNames.Data, out var data).ShouldBeTrue();
        data.GetProperty("orderId").GetInt32().ShouldBe(1);
    }

    [Fact]
    public void Serialize_OmittedNullableFields_AreNotWritten()
    {
        // Only the always-set fields and the explicitly-provided ones should appear. The new
        // nullable fields default to null and must be omitted so Dapr fills them in on publish.
        var ce = new CloudEvent<OrderData>(new OrderData { OrderId = 7 })
        {
            Source = new Uri("urn:checkout"),
            Type = "com.dapr.event.sent",
            Subject = "order/99",
        };

        var json = JsonSerializer.Serialize(ce, WebOptions);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty(CloudEventPropertyNames.Id, out _).ShouldBeFalse();
        root.TryGetProperty(CloudEventPropertyNames.SpecVersion, out _).ShouldBeFalse();
        root.TryGetProperty(CloudEventPropertyNames.Time, out _).ShouldBeFalse();
        root.TryGetProperty(CloudEventPropertyNames.TraceId, out _).ShouldBeFalse();
        root.TryGetProperty(CloudEventPropertyNames.TraceParent, out _).ShouldBeFalse();
        root.TryGetProperty(CloudEventPropertyNames.TraceState, out _).ShouldBeFalse();

        // Provided and always-set fields are still emitted.
        AssertString(root, CloudEventPropertyNames.Source, ce.Source.ToString());
        AssertString(root, CloudEventPropertyNames.Type, ce.Type);
        AssertString(root, CloudEventPropertyNames.Subject, ce.Subject);
        AssertString(root, CloudEventPropertyNames.DataContentType, ce.DataContentType);
    }

    private static void AssertString(JsonElement root, string name, string expected)
    {
        root.TryGetProperty(name, out var prop).ShouldBeTrue(
            $"expected property '{name}' on serialized CloudEvent");
        prop.GetString().ShouldBe(expected);
    }
}
