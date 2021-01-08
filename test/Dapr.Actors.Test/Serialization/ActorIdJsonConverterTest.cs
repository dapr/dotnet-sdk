// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Actors.Client;
using Dapr.Actors.Test;
using Xunit;

namespace Dapr.Actors.Serialization
{
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
            var id = ActorId.CreateRandom().GetId();
            var document = $@"
{{
    ""actor"": null
}}";
            
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

        [Fact]
        public async void CanRoundTripActorReference_Remoting()
        {
            // Create an actor Id.
            var actorId1 = new ActorId("abc");
            var actorId2 = new ActorId("xyz");

            // Make strongly typed Actor calls with Remoting.
            // DemoActor is the type registered with Dapr runtime in the service.
            var proxy1 = ActorProxy.Create<ITestActor>(actorId1, "TestActor");
            var proxy2 = ActorProxy.Create<ITestActor>(actorId2, "TestActor");

            var reference = new ActorReference()
            {
                ActorId = actorId1,
                ActorType = "TestActor",
            };

            await proxy2.SetCallingActorId(reference);
        }

        private class ActorHolder
        {
            [JsonPropertyName("actor")]
            public ActorId Actor { get; set; }
        }
    }
}
