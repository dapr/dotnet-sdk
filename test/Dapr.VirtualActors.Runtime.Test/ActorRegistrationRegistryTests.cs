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

public class ActorRegistrationRegistryTests
{
    [Fact]
    public void Register_AddsRegistration()
    {
        var registry = new ActorRegistrationRegistry();
        var typeInfo = new ActorTypeInformation("TestActor", typeof(GreeterActor), []);
        var registration = new ActorRegistration(typeInfo, (host, _) => new GreeterActor(host));

        registry.Register(registration);

        registry.RegisteredActorTypes.Count.ShouldBe(1);
        registry.RegisteredActorTypes.ShouldContain("TestActor");
    }

    [Fact]
    public void Register_DuplicateName_Throws()
    {
        var registry = new ActorRegistrationRegistry();
        var typeInfo = new ActorTypeInformation("TestActor", typeof(GreeterActor), []);
        var registration = new ActorRegistration(typeInfo, (host, _) => new GreeterActor(host));

        registry.Register(registration);

        Should.Throw<InvalidOperationException>(() => registry.Register(registration));
    }

    [Fact]
    public void GetRegistration_ExistingType_Returns()
    {
        var registry = new ActorRegistrationRegistry();
        var typeInfo = new ActorTypeInformation("TestActor", typeof(GreeterActor), []);
        var registration = new ActorRegistration(typeInfo, (host, _) => new GreeterActor(host));
        registry.Register(registration);

        var result = registry.GetRegistration("TestActor");

        result.ShouldBe(registration);
    }

    [Fact]
    public void GetRegistration_NonExistingType_Throws()
    {
        var registry = new ActorRegistrationRegistry();

        Should.Throw<InvalidOperationException>(() => registry.GetRegistration("NonExistent"));
    }

    [Fact]
    public void Registrations_ReturnsAllRegistered()
    {
        var registry = new ActorRegistrationRegistry();
        var typeInfo1 = new ActorTypeInformation("Actor1", typeof(GreeterActor), []);
        var typeInfo2 = new ActorTypeInformation("Actor2", typeof(GreeterActor), []);
        registry.Register(new ActorRegistration(typeInfo1, (host, _) => new GreeterActor(host)));
        registry.Register(new ActorRegistration(typeInfo2, (host, _) => new GreeterActor(host)));

        registry.Registrations.Count.ShouldBe(2);
    }
}
