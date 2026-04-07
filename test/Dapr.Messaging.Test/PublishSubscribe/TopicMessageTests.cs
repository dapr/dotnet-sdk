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
// ------------------------------------------------------------------------

using Dapr.Messaging.PublishSubscribe;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.Messaging.Test.PublishSubscribe;

public sealed class TopicMessageTests
{
    [Fact]
    public void Constructor_SetsAllPositionalProperties()
    {
        var msg = new TopicMessage("id-1", "source-1", "type-1", "1.0", "application/json", "topic-1", "pubsub-1");

        Assert.Equal("id-1", msg.Id);
        Assert.Equal("source-1", msg.Source);
        Assert.Equal("type-1", msg.Type);
        Assert.Equal("1.0", msg.SpecVersion);
        Assert.Equal("application/json", msg.DataContentType);
        Assert.Equal("topic-1", msg.Topic);
        Assert.Equal("pubsub-1", msg.PubSubName);
    }

    [Fact]
    public void DefaultPath_IsNull()
    {
        var msg = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub");
        Assert.Null(msg.Path);
    }

    [Fact]
    public void DefaultData_IsEmpty()
    {
        var msg = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub");
        Assert.Equal(0, msg.Data.Length);
    }

    [Fact]
    public void DefaultExtensions_IsEmptyDictionary()
    {
        var msg = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub");
        Assert.NotNull(msg.Extensions);
        Assert.Empty(msg.Extensions);
    }

    [Fact]
    public void Path_CanBeSetViaInit()
    {
        var msg = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub")
        {
            Path = "/custom/path"
        };
        Assert.Equal("/custom/path", msg.Path);
    }

    [Fact]
    public void Data_CanBeSetViaInit()
    {
        var payload = "hello world"u8.ToArray();
        var msg = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub")
        {
            Data = payload
        };
        Assert.Equal(payload, msg.Data.ToArray());
    }

    [Fact]
    public void Extensions_CanBeSetViaInit()
    {
        var extensions = new Dictionary<string, Value>
        {
            { "ext1", Value.ForString("val1") }
        };
        var msg = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub")
        {
            Extensions = extensions
        };
        Assert.Single(msg.Extensions);
        Assert.Equal("val1", msg.Extensions["ext1"].StringValue);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Share the same Extensions instance so record equality works (Dictionary does not override Equals).
        var sharedExtensions = new Dictionary<string, Value>();
        var a = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub") { Extensions = sharedExtensions };
        var b = new TopicMessage("id", "src", "type", "1.0", "text/plain", "topic", "pubsub") { Extensions = sharedExtensions };
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentId_AreNotEqual()
    {
        var a = new TopicMessage("id-a", "src", "type", "1.0", "text/plain", "topic", "pubsub");
        var b = new TopicMessage("id-b", "src", "type", "1.0", "text/plain", "topic", "pubsub");
        Assert.NotEqual(a, b);
    }
}
