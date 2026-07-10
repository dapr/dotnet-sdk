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

using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class DescriptorTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void ActorDescriptors_UseRecordEquality()
    {
        var parameters = new[]
        {
            new ActorParameterDescriptor("item", typeof(string), 0, false, false, null),
            new ActorParameterDescriptor("ct", typeof(CancellationToken), 1, true, true, default(CancellationToken)),
        };
        var methods = new[]
        {
            new ActorMethodDescriptor("AddItem", "AddItem", typeof(Task), parameters),
        };

        var left = new ActorTypeDescriptor("CartActor", 1, typeof(TestActor), typeof(ITestActor), methods);
        var right = new ActorTypeDescriptor("CartActor", 1, typeof(TestActor), typeof(ITestActor), methods);
        var different = right with { ContractVersion = 2 };

        Assert.Equal(left, right);
        Assert.NotEqual(left, different);
        Assert.Equal(methods[0], new ActorMethodDescriptor("AddItem", "AddItem", typeof(Task), parameters));
        Assert.Equal(parameters[0], new ActorParameterDescriptor("item", typeof(string), 0, false, false, null));
    }

    private interface ITestActor : IActor
    {
    }

    private sealed class TestActor : Actor
    {
        protected override ActorId Id => ActorId.Create("test");

        protected override IActorStateAccessor State => throw new NotSupportedException();
    }
}
