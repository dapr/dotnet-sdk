// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System.Text.Json;
using Dapr.Workflow.Serialization;

namespace Dapr.Workflow.Test.Serialziation;

public class JsonWorkflowSerializerTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonWorkflowSerializer(null!));
    }

    [Fact]
    public void Serialize_ShouldReturnEmptyString_WhenValueIsNull()
    {
        var serializer = new JsonWorkflowSerializer();

        var json = serializer.Serialize(null);

        Assert.Equal(string.Empty, json);
    }

    [Fact]
    public void Deserialize_Generic_ShouldReturnDefault_WhenDataIsNull()
    {
        var serializer = new JsonWorkflowSerializer();

        var value = serializer.Deserialize<int>(null);

        Assert.Equal(default, value);
    }

    [Fact]
    public void Deserialize_Generic_ShouldReturnDefault_WhenDataIsEmpty()
    {
        var serializer = new JsonWorkflowSerializer();

        var value = serializer.Deserialize<SimplePayload>(string.Empty);

        Assert.Null(value);
    }

    [Fact]
    public void Deserialize_NonGeneric_ShouldThrowArgumentNullException_WhenReturnTypeIsNull()
    {
        var serializer = new JsonWorkflowSerializer();

        Assert.Throws<ArgumentNullException>(() => serializer.Deserialize("{}", null!));
    }

    [Fact]
    public void Deserialize_NonGeneric_ShouldReturnNull_WhenDataIsNull()
    {
        var serializer = new JsonWorkflowSerializer();

        var value = serializer.Deserialize(null, typeof(SimplePayload));

        Assert.Null(value);
    }

    [Fact]
    public void Deserialize_NonGeneric_ShouldReturnNull_WhenDataIsEmpty()
    {
        var serializer = new JsonWorkflowSerializer();

        var value = serializer.Deserialize(string.Empty, typeof(SimplePayload));

        Assert.Null(value);
    }

    [Fact]
    public void Serialize_DefaultOptions_ShouldUseCamelCasePropertyNames()
    {
        var serializer = new JsonWorkflowSerializer();

        var json = serializer.Serialize(new SimplePayload { FirstName = "Ada" });

        Assert.Contains("\"firstName\"", json);
        Assert.DoesNotContain("\"FirstName\"", json);
    }

    [Fact]
    public void Serialize_CustomOptions_ShouldRespectPropertyNamingPolicy()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null
        };
        var serializer = new JsonWorkflowSerializer(options);

        var json = serializer.Serialize(new SimplePayload { FirstName = "Ada" });

        Assert.Contains("\"FirstName\"", json);
        Assert.DoesNotContain("\"firstName\"", json);
    }

    [Fact]
    public void Serialize_WithInputType_ShouldSerializeUsingProvidedTypeHint()
    {
        var serializer = new JsonWorkflowSerializer();

        var value = new DerivedPayload { BaseValue = "base", ExtraValue = "extra" };

        var jsonAsRuntimeType = serializer.Serialize(value);
        var jsonAsBaseType = serializer.Serialize(value, typeof(BasePayload));

        Assert.Contains("extraValue", jsonAsRuntimeType);
        Assert.DoesNotContain("extraValue", jsonAsBaseType);
        Assert.Contains("baseValue", jsonAsBaseType);
    }

    [Fact]
    public void SerializeAndDeserialize_Generic_ShouldRoundTripObject()
    {
        var serializer = new JsonWorkflowSerializer();

        var original = new ComplexPayload
        {
            Id = 123,
            Name = "workflow",
            Nested = new SimplePayload { FirstName = "Ada" }
        };

        var json = serializer.Serialize(original);
        var roundTripped = serializer.Deserialize<ComplexPayload>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Id, roundTripped!.Id);
        Assert.Equal(original.Name, roundTripped.Name);
        Assert.NotNull(roundTripped.Nested);
        Assert.Equal(original.Nested.FirstName, roundTripped.Nested.FirstName);
    }
    
    [Fact]
    public void Deserialize_NonGeneric_ShouldReturnExpectedObject_WhenDataIsValid()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var json = "{\"firstName\":\"Ada\"}";

        var obj = serializer.Deserialize(json, typeof(SimplePayload));

        Assert.NotNull(obj);
        var payload = Assert.IsType<SimplePayload>(obj);
        Assert.Equal("Ada", payload.FirstName);
    }

    [Fact]
    public void Deserialize_NonGeneric_ShouldThrowJsonException_WhenDataIsInvalidJson()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.ThrowsAny<JsonException>(() => serializer.Deserialize("{not-json", typeof(SimplePayload)));
    }

    private sealed class SimplePayload
    {
        public string? FirstName { get; set; }
    }

    private class BasePayload
    {
        public string? BaseValue { get; set; }
    }

    private sealed class DerivedPayload : BasePayload
    {
        public string? ExtraValue { get; set; }
    }

    private sealed class ComplexPayload
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public SimplePayload? Nested { get; set; }
    }
}
