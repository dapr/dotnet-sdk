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

namespace Dapr.VirtualActors.Generators.Test;

public class VirtualActorRegistrationGeneratorTests
{
    [Fact]
    public void NoActors_GeneratesNoSource()
    {
        var source = @"
namespace TestApp
{
    public class NotAnActor
    {
        public void DoWork() { }
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorRegistrationGenerator>(compilation);

        // Should not generate any source files (only diagnostics)
        result.GeneratedTrees.Length.ShouldBe(0);
    }

    [Fact]
    public void SingleActor_WithHostOnlyCtor_GeneratesRegistration()
    {
        var source = @"
using Dapr.VirtualActors;
using Dapr.VirtualActors.Runtime;

namespace TestApp
{
    public class MyActor : VirtualActor
    {
        public MyActor(VirtualActorHost host) : base(host) { }
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorRegistrationGenerator>(compilation);

        result.GeneratedTrees.Length.ShouldBe(1);
        var generatedSource = result.GeneratedTrees[0].GetText().ToString();

        generatedSource.ShouldContain("MyActor");
        generatedSource.ShouldContain("RegisterDiscoveredActors");
        generatedSource.ShouldContain("new global::TestApp.MyActor(host)");
    }

    [Fact]
    public void ActorWithoutHostOnlyCtor_GeneratesThrowingFactory()
    {
        var source = @"
using Dapr.VirtualActors;
using Dapr.VirtualActors.Runtime;

namespace TestApp
{
    public class ComplexActor : VirtualActor
    {
        public ComplexActor(VirtualActorHost host, string extraParam) : base(host) { }
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorRegistrationGenerator>(compilation);

        result.GeneratedTrees.Length.ShouldBe(1);
        var generatedSource = result.GeneratedTrees[0].GetText().ToString();

        generatedSource.ShouldContain("ComplexActor");
        generatedSource.ShouldContain("InvalidOperationException");
    }

    [Fact]
    public void AbstractActor_IsNotRegistered()
    {
        var source = @"
using Dapr.VirtualActors;
using Dapr.VirtualActors.Runtime;

namespace TestApp
{
    public abstract class BaseActor : VirtualActor
    {
        protected BaseActor(VirtualActorHost host) : base(host) { }
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorRegistrationGenerator>(compilation);

        // Abstract actors should not be registered
        result.GeneratedTrees.Length.ShouldBe(0);
    }

    [Fact]
    public void MultipleActors_GeneratesAllRegistrations()
    {
        var source = @"
using Dapr.VirtualActors;
using Dapr.VirtualActors.Runtime;

namespace TestApp
{
    public class ActorA : VirtualActor
    {
        public ActorA(VirtualActorHost host) : base(host) { }
    }

    public class ActorB : VirtualActor
    {
        public ActorB(VirtualActorHost host) : base(host) { }
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorRegistrationGenerator>(compilation);

        result.GeneratedTrees.Length.ShouldBe(1);
        var generatedSource = result.GeneratedTrees[0].GetText().ToString();

        generatedSource.ShouldContain("ActorA");
        generatedSource.ShouldContain("ActorB");
    }
}
