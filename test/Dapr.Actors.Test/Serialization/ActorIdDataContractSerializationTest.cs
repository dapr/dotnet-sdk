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

using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using Shouldly;
using Xunit;

namespace Dapr.Actors.Serialization;

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
        xmlDoc.DocumentElement.Name.ShouldBe("ActorId");
        xmlDoc.DocumentElement.InnerText.ShouldBe(id.ToString());
        ms.Position = 0;

        var deserializedActorId = serializer.ReadObject(ms) as ActorId;
        deserializedActorId.ShouldBe(id);
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
        xmlDoc.DocumentElement.Name.ShouldBe("ActorId");
        xmlDoc.DocumentElement.InnerText.ShouldBe(string.Empty);

        ms.Position = 0;
        var deserializedActorId = serializer.ReadObject(ms) as ActorId;
        deserializedActorId.ShouldBe(id);
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
        deserializedActorRef.ActorId.ShouldBe(actorId);
        deserializedActorRef.ActorType.ShouldBe("TestActor");
    }
}
