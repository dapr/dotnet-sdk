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

using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Dapr.Actors.Serialization;

public class ActorIdJsonConverterTest
{
    [Fact]
    public void CanSerializeActorId()
    {
        var id = ActorId.CreateRandom();
        var document = new { actor = id, };

        // We use strings for ActorId - the result should be the same as passing the Id directly.
        var expected = JsonSerializer.Serialize(new { actor = id.GetId(), });
            
        var serialized = JsonSerializer.Serialize(document);

        Assert.Equal(expected, serialized);
    }

    [Fact]
    public void CanSerializeNullActorId()
    {
        var document = new { actor = (ActorId)null, };

        var expected = JsonSerializer.Serialize(new { actor = (string)null, });
            
        var serialized = JsonSerializer.Serialize(document);

        Assert.Equal(expected, serialized);
    }

    [Fact]
    public void CanDeserializeActorId()
    {
        var id = ActorId.CreateRandom().GetId();
        var document = $@"
            {{
                ""actor"": ""{id}""
            }}";
            
        var deserialized = JsonSerializer.Deserialize<ActorHolder>(document);

        Assert.Equal(id, deserialized.Actor.GetId());
    }

    [Fact]
    public void CanDeserializeNullActorId()
    {
        const string document = @"
            {
                ""actor"": null
            }";
            
        var deserialized = JsonSerializer.Deserialize<ActorHolder>(document);

        Assert.Null(deserialized.Actor);
    }

    [Theory]
    [InlineData("{ \"actor\": ")]
    [InlineData("{ \"actor\": \"hi")]
    [InlineData("{ \"actor\": }")]
    [InlineData("{ \"actor\": 3 }")]
    [InlineData("{ \"actor\": \"\"}")]
    [InlineData("{ \"actor\": \"        \"}")]
    public void CanReportErrorsFromInvalidData(string document)
    {
        // The error messages are provided by the serializer, don't test them here
        // that would be fragile.
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<ActorHolder>(document);
        });
    }

    // Regression test for #444
    [Fact]
    public void CanRoundTripActorReference()
    {
        var reference = new ActorReference()
        {
            ActorId = ActorId.CreateRandom(),
            ActorType = "TestActor",
        };

        var serialized = JsonSerializer.Serialize(reference);
        var deserialized = JsonSerializer.Deserialize<ActorReference>(serialized);

        Assert.Equal(reference.ActorId.GetId(), deserialized.ActorId.GetId());
        Assert.Equal(reference.ActorType, deserialized.ActorType);
    }

    private class ActorHolder
    {
        [JsonPropertyName("actor")]
        public ActorId Actor { get; set; }
    }
}
