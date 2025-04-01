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

namespace Dapr.Actors;

using System.IO;
using System.Runtime.Serialization;
using System.Xml;

[DataContract(Name = "ActorInvokeExceptionData", Namespace = Constants.Namespace)]
internal class ActorInvokeExceptionData
{
    private static readonly DataContractSerializer ActorInvokeExceptionDataSerializer = new DataContractSerializer(typeof(ActorInvokeExceptionData));

    public ActorInvokeExceptionData(string type, string message)
    {
        this.Type = type;
        this.Message = message;
    }

    [DataMember]
    public string Type { get; private set; }

    [DataMember]
    public string Message { get; private set; }

    internal static ActorInvokeExceptionData Deserialize(Stream stream)
    {
        if ((stream == null) || (stream.Length == 0))
        {
            return null;
        }

        using var reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
        return (ActorInvokeExceptionData)ActorInvokeExceptionDataSerializer.ReadObject(reader);
    }

    internal byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
        ActorInvokeExceptionDataSerializer.WriteObject(writer, this);
        writer.Flush();
        return stream.ToArray();
    }
}