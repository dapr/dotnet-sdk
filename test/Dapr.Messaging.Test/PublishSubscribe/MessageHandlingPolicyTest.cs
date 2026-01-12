// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

public class MessageHandlingPolicyTest
{
    [Fact]
    public void Test_MessageHandlingPolicy_Constructor()
    {
        var timeoutDuration = TimeSpan.FromMilliseconds(2000);
        const TopicResponseAction defaultResponseAction = TopicResponseAction.Drop;

        var policy = new MessageHandlingPolicy(timeoutDuration, defaultResponseAction);

        Assert.Equal(timeoutDuration, policy.TimeoutDuration);
        Assert.Equal(defaultResponseAction, policy.DefaultResponseAction);
    }

    [Fact]
    public void Test_MessageHandlingPolicy_Equality()
    {
        var timeSpan1 = TimeSpan.FromMilliseconds(1000);
        var timeSpan2 = TimeSpan.FromMilliseconds(2000);

        var policy1 = new MessageHandlingPolicy(timeSpan1, TopicResponseAction.Success);
        var policy2 = new MessageHandlingPolicy(timeSpan1, TopicResponseAction.Success);
        var policy3 = new MessageHandlingPolicy(timeSpan2, TopicResponseAction.Retry);

        Assert.Equal(policy1, policy2); // Value Equality
        Assert.NotEqual(policy1, policy3); // Different values
    }

    [Fact]
    public void Test_MessageHandlingPolicy_Immutability()
    {
        var timeoutDuration = TimeSpan.FromMilliseconds(2000);
        const TopicResponseAction defaultResponseAction = TopicResponseAction.Drop;

        var policy1 = new MessageHandlingPolicy(timeoutDuration, defaultResponseAction);

        var newTimeoutDuration = TimeSpan.FromMilliseconds(3000);
        const TopicResponseAction newDefaultResponseAction = TopicResponseAction.Retry;

        // Creating a new policy with different values.
        var policy2 = policy1 with
        {
            TimeoutDuration = newTimeoutDuration, DefaultResponseAction = newDefaultResponseAction
        };

        // Asserting that original policy is unaffected by changes made to new policy.
        Assert.Equal(timeoutDuration, policy1.TimeoutDuration);
        Assert.Equal(defaultResponseAction, policy1.DefaultResponseAction);
    }
}