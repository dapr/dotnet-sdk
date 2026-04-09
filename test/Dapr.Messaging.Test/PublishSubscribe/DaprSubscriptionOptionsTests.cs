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

namespace Dapr.Messaging.Test.PublishSubscribe;

public sealed class DaprSubscriptionOptionsTests
{
    private static readonly MessageHandlingPolicy DefaultPolicy =
        new(TimeSpan.FromSeconds(5), TopicResponseAction.Success);

    [Fact]
    public void Constructor_SetsMessageHandlingPolicy()
    {
        var policy = new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry);
        var options = new DaprSubscriptionOptions(policy);
        Assert.Equal(policy, options.MessageHandlingPolicy);
    }

    [Fact]
    public void DefaultMetadata_IsEmptyDictionary()
    {
        var options = new DaprSubscriptionOptions(DefaultPolicy);
        Assert.NotNull(options.Metadata);
        Assert.Empty(options.Metadata);
    }

    [Fact]
    public void DefaultDeadLetterTopic_IsNull()
    {
        var options = new DaprSubscriptionOptions(DefaultPolicy);
        Assert.Null(options.DeadLetterTopic);
    }

    [Fact]
    public void DefaultMaximumQueuedMessages_IsNull()
    {
        var options = new DaprSubscriptionOptions(DefaultPolicy);
        Assert.Null(options.MaximumQueuedMessages);
    }

    [Fact]
    public void DefaultMaximumCleanupTimeout_Is30Seconds()
    {
        var options = new DaprSubscriptionOptions(DefaultPolicy);
        Assert.Equal(TimeSpan.FromSeconds(30), options.MaximumCleanupTimeout);
    }

    [Fact]
    public void DeadLetterTopic_CanBeSet()
    {
        var options = new DaprSubscriptionOptions(DefaultPolicy) { DeadLetterTopic = "my-dead-letter" };
        Assert.Equal("my-dead-letter", options.DeadLetterTopic);
    }

    [Fact]
    public void MaximumQueuedMessages_CanBeSet()
    {
        var options = new DaprSubscriptionOptions(DefaultPolicy) { MaximumQueuedMessages = 50 };
        Assert.Equal(50, options.MaximumQueuedMessages);
    }

    [Fact]
    public void MaximumCleanupTimeout_CanBeSet()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var options = new DaprSubscriptionOptions(DefaultPolicy) { MaximumCleanupTimeout = timeout };
        Assert.Equal(timeout, options.MaximumCleanupTimeout);
    }

    [Fact]
    public void Metadata_CanBeSet()
    {
        var meta = new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } };
        var options = new DaprSubscriptionOptions(DefaultPolicy) { Metadata = meta };
        Assert.Equal(2, options.Metadata.Count);
        Assert.Equal("v1", options.Metadata["k1"]);
        Assert.Equal("v2", options.Metadata["k2"]);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var policy = new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Drop);
        // Share the same Metadata instance so record equality works (Dictionary does not override Equals).
        var sharedMeta = new Dictionary<string, string>();
        var a = new DaprSubscriptionOptions(policy) { DeadLetterTopic = "dlq", MaximumQueuedMessages = 10, Metadata = sharedMeta };
        var b = new DaprSubscriptionOptions(policy) { DeadLetterTopic = "dlq", MaximumQueuedMessages = 10, Metadata = sharedMeta };
        Assert.Equal(a, b);
    }

    [Fact]
    public void WithExpression_ProducesNewInstanceWithUpdatedValues()
    {
        var original = new DaprSubscriptionOptions(DefaultPolicy) { DeadLetterTopic = "original-dlq" };
        var updated = original with { DeadLetterTopic = "updated-dlq" };

        Assert.Equal("original-dlq", original.DeadLetterTopic);
        Assert.Equal("updated-dlq", updated.DeadLetterTopic);
        Assert.Equal(original.MessageHandlingPolicy, updated.MessageHandlingPolicy);
    }
}
