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

using Shouldly;
using Xunit;

namespace Dapr.VirtualActors.Runtime.Test;

public class VirtualActorOptionsTests
{
    [Fact]
    public void RegisterActor_WithFactory_AddsRegistration()
    {
        var options = new VirtualActorOptions();

        options.RegisterActor<GreeterActor>(host => new GreeterActor(host));

        options.ActorRegistrations.Count.ShouldBe(1);
        options.ActorRegistrations[0].TypeInformation.ActorTypeName.ShouldBe("GreeterActor");
        options.ActorRegistrations[0].Factory.ShouldNotBeNull();
    }

    [Fact]
    public void RegisterActor_WithCustomName_UsesCustomName()
    {
        var options = new VirtualActorOptions();

        options.RegisterActor<GreeterActor>(
            host => new GreeterActor(host),
            actorTypeName: "MyCustomGreeter");

        options.ActorRegistrations[0].TypeInformation.ActorTypeName.ShouldBe("MyCustomGreeter");
    }

    [Fact]
    public void RegisterActor_WithServiceProviderFactory_AddsRegistration()
    {
        var options = new VirtualActorOptions();

        options.RegisterActor<GreeterActor>(
            (host, sp) => new GreeterActor(host));

        options.ActorRegistrations.Count.ShouldBe(1);
    }

    [Fact]
    public void Reentrancy_DefaultsToDisabled()
    {
        var options = new VirtualActorOptions();
        options.Reentrancy.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void ActorIdleTimeout_DefaultsToNull()
    {
        var options = new VirtualActorOptions();
        options.ActorIdleTimeout.ShouldBeNull();
    }

    [Fact]
    public void ActorRegistrations_IsReadOnly()
    {
        var options = new VirtualActorOptions();
        options.ActorRegistrations.ShouldBeAssignableTo<IReadOnlyList<ActorRegistration>>();
    }
}
