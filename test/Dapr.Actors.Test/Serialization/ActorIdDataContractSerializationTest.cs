// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Dapr.Actors.Serialization
{
    public class ActorReferenceDataContractSerializationTest
    {
        [Fact]
        public void CanSerializeAndDeserializeActorId()
        {
            var id = ActorId.CreateRandom();
            DataContractSerializer serializer = new DataContractSerializer(id.GetType());
            MemoryStream ms = new MemoryStream();
            serializer.WriteObject(ms, id);
            ms.Position = 0;

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(ms);
            xmlDoc.DocumentElement.Name.Should().Be("ActorId");
            xmlDoc.DocumentElement.InnerText.Should().Be(id.ToString());
            ms.Position = 0;

            var deserializedActorId = serializer.ReadObject(ms) as ActorId;
            deserializedActorId.Should().Be(id);
        }

        [Fact]
        public void CanSerializeAndDeserializeNullActorId()
        {
            ActorId id = null;
            DataContractSerializer serializer = new DataContractSerializer(typeof(ActorId));
            MemoryStream ms = new MemoryStream();
            serializer.WriteObject(ms, id);
            ms.Position = 0;

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(ms);
            xmlDoc.DocumentElement.Name.Should().Be("ActorId");
            xmlDoc.DocumentElement.InnerText.Should().Be(string.Empty);

            ms.Position = 0;
            var deserializedActorId = serializer.ReadObject(ms) as ActorId;
            deserializedActorId.Should().Be(id);
        }

        [Fact]
        public void CanRoundTripActorReference()
        {
            var actorId = new ActorId("abc");
            var actorReference = new ActorReference()
            {
                ActorId = actorId,
                ActorType = "TestActor"
            };

            DataContractSerializer serializer = new DataContractSerializer(actorReference.GetType());
            MemoryStream ms = new MemoryStream();
            serializer.WriteObject(ms, actorReference);
            ms.Position = 0;

            var deserializedActorRef = serializer.ReadObject(ms) as ActorReference;
            deserializedActorRef.ActorId.Should().Be(actorId);
            deserializedActorRef.ActorType.Should().Be("TestActor");
        }
    }
}
