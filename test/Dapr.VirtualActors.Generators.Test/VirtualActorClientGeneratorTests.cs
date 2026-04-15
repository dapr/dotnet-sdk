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

public class VirtualActorClientGeneratorTests
{
    [Fact]
    public void InterfaceWithGenerateAttribute_GeneratesClient()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Dapr.VirtualActors;
using Dapr.VirtualActors.Generators;

namespace TestApp
{
    [GenerateActorClient]
    public interface IMyActor : IVirtualActor
    {
        Task<string> GetNameAsync(CancellationToken ct = default);
        Task SetNameAsync(string name, CancellationToken ct = default);
        Task DoWorkAsync();
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorClientGenerator>(compilation);

        // Should generate the attribute source + the client source
        result.GeneratedTrees.Length.ShouldBeGreaterThanOrEqualTo(2); // attributes + client

        var sources = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToList();

        // Find the client source (not the attribute sources)
        var clientSource = sources.FirstOrDefault(s => s.Contains("MyActorClient"));
        clientSource.ShouldNotBeNull("Expected to find generated MyActorClient class");
        clientSource.ShouldContain("GetNameAsync");
        clientSource.ShouldContain("SetNameAsync");
        clientSource.ShouldContain("DoWorkAsync");
        clientSource.ShouldContain("IVirtualActorProxy");
    }

    [Fact]
    public void InterfaceWithoutAttribute_GeneratesNoClient()
    {
        var source = @"
using System.Threading.Tasks;
using Dapr.VirtualActors;

namespace TestApp
{
    public interface IMyActor : IVirtualActor
    {
        Task DoWorkAsync();
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorClientGenerator>(compilation);

        // Should only generate the attribute sources (no client)
        var clientSources = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .Where(s => s.Contains("MyActorClient"))
            .ToList();

        clientSources.ShouldBeEmpty();
    }

    [Fact]
    public void InterfaceWithCustomClientName_UsesCustomName()
    {
        var source = @"
using System.Threading.Tasks;
using Dapr.VirtualActors;
using Dapr.VirtualActors.Generators;

namespace TestApp
{
    [GenerateActorClient(Name = ""CustomProxy"")]
    public interface IMyActor : IVirtualActor
    {
        Task DoWorkAsync();
    }
}";
        var compilation = TestCompilationHelper.CreateCompilation(source);
        var result = TestCompilationHelper.RunGenerator<VirtualActorClientGenerator>(compilation);

        var sources = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToList();

        var clientSource = sources.FirstOrDefault(s => s.Contains("CustomProxy"));
        clientSource.ShouldNotBeNull("Expected to find generated CustomProxy class");
    }
}
