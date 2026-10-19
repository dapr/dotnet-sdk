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

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.SourceGenerators.OtherSample;
using Dapr.Actors.Next.SourceGenerators.Sample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using First = Dapr.Actors.Next.SourceGenerators.OtherSample.First;
using Second = Dapr.Actors.Next.SourceGenerators.OtherSample.Second;

namespace Dapr.Actors.Next.SourceGenerators.Test;

public sealed partial class GeneratorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Equivalent_inherited_actor_methods_are_emitted_once(bool redeclare, bool scanReferences)
    {
        var source = $$"""
                     using System.Threading.Tasks;
                     using Dapr.Actors.Next.Abstractions;
                     using Dapr.Actors.Next.Abstractions.Attributes;
                     using Dapr.Actors.Next.Abstractions.State;
                     public interface ILeft : IActor { Task Ping(); }
                     public interface IRight : IActor { Task Ping(); }
                     public interface IDiamond : ILeft, IRight {}
                     [GenerateActorClient]
                     public interface IMerged : IDiamond, ILeft { {{(redeclare ? "new Task Ping();" : "")}} }
                     [DaprActor]
                     public sealed class Merged : Actor, IMerged {
                         protected override ActorId Id => new("one");
                         protected override IActorStateAccessor State => throw new System.NotImplementedException();
                         public Task Ping() => Task.CompletedTask;
                     }
                     """;
        var compilation = CreateCompilation("Merged", source);
        if (scanReferences)
        {
            using var image = new MemoryStream();
            var emitted = compilation.Emit(image);
            Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
            compilation = CreateCompilation("MergedApp", "public class Marker {}",
                MetadataReference.CreateFromImage(image.ToArray()));
        }
        var generated = RunGenerator(compilation, scanReferences, assertGeneratedCompiles: true);
        Assert.Equal(1, CountOccurrences(generated, "case @\"Ping\":"));
        Assert.Equal(1, CountOccurrences(generated, "Task Ping("));
        Assert.Equal(generated, RunGenerator(compilation, scanReferences, assertGeneratedCompiles: true));
    }

    [Fact]
    public void Actor_implementing_separate_contracts_cannot_overload_a_wire_operation()
    {
        const string source = """
                              using System.Threading.Tasks;
                              using Dapr.Actors.Next.Abstractions;
                              using Dapr.Actors.Next.Abstractions.Attributes;
                              using Dapr.Actors.Next.Abstractions.State;
                              [GenerateActorClient] public interface ILeft : IActor { Task Ping(int value); }
                              [GenerateActorClient] public interface IRight : IActor { Task Ping(string value); }
                              [DaprActor] public sealed class CollisionActor : Actor, ILeft, IRight {
                                  protected override ActorId Id => new("one");
                                  protected override IActorStateAccessor State => throw new System.NotImplementedException();
                                  public Task Ping(int value) => Task.CompletedTask;
                                  public Task Ping(string value) => Task.CompletedTask;
                              }
                              """;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ActorsNextSourceGenerator().AsSourceGenerator()],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(false));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation("SeparateContracts", source), out var output, out var diagnostics);
        var diagnostic = Assert.Single(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Equal("DAPRGEN001", diagnostic.Id);
        Assert.Contains("CollisionActor", diagnostic.GetMessage());
        Assert.Empty(output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain("class CollisionActorDispatcher", driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString());
    }

    [Theory]
    [InlineData("Task Ping(int value);", "Task Ping(string value);")]
    [InlineData("Task<int> Ping();", "Task<string> Ping();")]
    [InlineData("Task Ping(); Task Ping(int value);", "")]
    [InlineData("Task Ping(int first, int second);", "Task Ping(int one, int two);")]
    public void Incompatible_wire_names_report_actionable_diagnostic_instead_of_broken_proxies(string left, string right)
    {
        var source = $$"""
                     using System.Threading.Tasks;
                     using Dapr.Actors.Next.Abstractions;
                     using Dapr.Actors.Next.Abstractions.Attributes;
                     public interface ILeft : IActor { {{left}} }
                     public interface IRight : IActor { {{right}} }
                     [GenerateActorClient] public interface IMerged : ILeft, IRight {}
                     """;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ActorsNextSourceGenerator().AsSourceGenerator()],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(false));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation("Collision", source), out var output, out var diagnostics);
        var diagnostic = Assert.Single(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Equal("DAPRGEN001", diagnostic.Id);
        Assert.Contains("Ping", diagnostic.GetMessage());
        Assert.Contains("IMerged", diagnostic.GetMessage());
        Assert.Empty(output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(driver.GetRunResult().Results.Single().GeneratedSources);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unused_state_shapes_do_not_expand_large_member_graphs(bool invalidFamily)
    {
        const string stateSource = """
                                   public sealed class Wallet { public int Balance { get; set; } }
                                   public sealed class WalletV2 { public int Balance { get; set; } public bool Frozen { get; set; } }
                                   """;
        var source = new StringBuilder(stateSource);
        source.AppendLine("public sealed class Unrelated { public NodeP Root { get; set; } }");
        if (invalidFamily)
        {
            source.AppendLine("public sealed class UnrelatedV2 { public required string Other { get; set; } }");
        }

        source.AppendLine("public struct NodeA { public int Value; }");
        for (var name = 'B'; name <= 'P'; name++)
        {
            source.AppendLine($"public struct Node{name} {{ public Node{(char)(name - 1)} Left; public Node{(char)(name - 1)} Right; }}");
        }

        var expected = RunGenerator(CreateCompilation("UnusedShapes", stateSource), false, assertGeneratedCompiles: true);
        var compilation = CreateCompilation("UnusedShapes", source.ToString());
        // Warm Roslyn's symbol caches before measuring generator allocations.
        RunGenerator(compilation, false, assertGeneratedCompiles: true);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var generated = RunGenerator(compilation, false);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(expected, generated);
        Assert.True(allocatedBytes < 16 * 1024 * 1024, $"Generator allocated {allocatedBytes:N0} bytes for unused state shapes.");
    }

    [Theory]
    [InlineData("Task<System.Collections.Generic.List<int>>")]
    [InlineData("ValueTask<System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>>")]
    public void Nested_generic_results_and_parameter_names_compile(string returnType)
    {
        var source = $$"""
                       using System.Threading.Tasks;
                       using Dapr.Actors.Next.Abstractions;
                       using Dapr.Actors.Next.Abstractions.Attributes;
                       using Dapr.Actors.Next.Abstractions.State;
                       [GenerateActorClient]
                       public interface IContract : IActor
                       {
                           {{returnType}} Read(string payload);
                           Task Put(string request, string typed, string args, string wireSerializer, string @event, string payload, string payload1, string InvokeVoidAsync);
                       }
                       [DaprActor]
                       public sealed class Contract : Actor, IContract
                       {
                           protected override ActorId Id => new("one");
                           protected override IActorStateAccessor State => throw new System.NotImplementedException();
                           public {{returnType}} Read(string payload) => throw new System.NotImplementedException();
                           public Task Put(string request, string typed, string args, string wireSerializer, string @event, string payload, string payload1, string InvokeVoidAsync) => Task.CompletedTask;
                       }
                       """;
        RunGenerator(CreateCompilation("NestedResults", source), false, assertGeneratedCompiles: true);
    }

    [Fact]
    public void Same_simple_names_in_distinct_namespaces_compile_deterministically()
    {
        const string source = """
                              using System.Threading.Tasks;
                              using Dapr.Actors.Next.Abstractions;
                              using Dapr.Actors.Next.Abstractions.Attributes;
                              using Dapr.Actors.Next.Abstractions.State;
                              namespace First {
                                  [GenerateActorClient] public interface IAccount : IActor { Task Read(); }
                                  [DaprActor("First")] public sealed class Account : Actor, IAccount {
                                      protected override ActorId Id => new("one");
                                      protected override IActorStateAccessor State => throw new System.NotImplementedException();
                                      public Task Read() => Task.CompletedTask;
                                  }
                              }
                              namespace Second {
                                  [GenerateActorClient] public interface IAccount : IActor { Task Read(); }
                                  [DaprActor("Second")] public sealed class Account : Actor, IAccount {
                                      protected override ActorId Id => new("two");
                                      protected override IActorStateAccessor State => throw new System.NotImplementedException();
                                      public Task Read() => Task.CompletedTask;
                                  }
                              }
                              """;
        var compilation = CreateCompilation("Names", source);
        var first = RunGenerator(compilation, false, assertGeneratedCompiles: true);
        Assert.Equal(first, RunGenerator(compilation, false, assertGeneratedCompiles: true));
        Assert.Contains("GeneratedAccountProxy_First_IAccount", first);
        Assert.Contains("GeneratedAccountProxy_Second_IAccount", first);
    }

    [Fact]
    public void Inherited_members_are_copied_once_and_participate_in_shape_hash()
    {
        const string source = """
                              namespace Inheritance {
                                  public class Base {
                                      public int Balance { get; set; }
                                      public virtual string Owner { get; set; } = "";
                                      public int Revision;
                                  }
                                  public class WalletV1 : Base {
                                      public override string Owner { get; set; } = "";
                                      public new int Revision;
                                      public int Count { get; set; }
                                  }
                                  public class WalletV2 : Base {
                                      public override string Owner { get; set; } = "";
                                      public new int Revision;
                                      public int Count { get; set; }
                                      public bool Added { get; set; }
                                  }
                              }
                              """;
        var before = RunGenerator(CreateCompilation("InheritedShapes", source), false, assertGeneratedCompiles: true);
        Assert.Contains("Balance = source.Balance", before);
        Assert.Equal(1, CountOccurrences(before, "Owner = source.Owner"));
        Assert.Equal(1, CountOccurrences(before, "Revision = source.Revision"));
        var after = RunGenerator(CreateCompilation("InheritedShapes", source.Replace("int Balance", "long Balance")),
            false, assertGeneratedCompiles: true);
        var beforeHashes = Regex.Matches(before, "h1:[0-9a-f]+").Select(match => match.Value).ToArray();
        var afterHashes = Regex.Matches(after, "h1:[0-9a-f]+").Select(match => match.Value).ToArray();
        Assert.NotEmpty(beforeHashes);
        Assert.Equal(beforeHashes.Length, afterHashes.Length);
        Assert.All(beforeHashes.Zip(afterHashes), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generated_modules_compose_proxies_registries_and_migration_families(bool reverseOrder)
    {
        var modules = new[] { typeof(CalculatorActor).Module, typeof(First.AccountActor).Module };
        if (reverseOrder)
        {
            Array.Reverse(modules);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CalculatorDependency>();
        services.Configure<DaprActorsOptions>(options => options.EnableSidecarTransport = false);
        foreach (var module in modules)
        {
            RuntimeHelpers.RunModuleConstructor(module.ModuleHandle);
            var registration = module.Assembly.GetType("Dapr.Actors.Next.Generated.GeneratedActorRegistrationModule")!;
            registration.GetMethod("RegisterServices",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, [services, new DaprActorsOptions { EnableSidecarTransport = false }]);
        }

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var proxies = provider.GetRequiredService<IActorProxyFactory>();
        var calculator = proxies.Create<ICalculatorActor>(new ActorId("calculator"), "Calculator");
        Assert.Equal(6, await calculator.SumAsync(2, 3));
        Assert.Equal(new[] { new ActorId("one"), new ActorId("two") },
            await calculator.EchoAsync([new("one"), new("two")]));
        Assert.Equal(new[] { 7 }, (await calculator.MapAsync("numbers", 7))["numbers"]);

        var second = proxies.Create<Second.IAccount>(new ActorId("second"), "SecondAccount");
        Assert.Equal("a|b|c|d|e|f", await ((Second.ILeftAccount)second).EchoAsync("a", "b", "c", "d", "e", "f"));
        Assert.Equal("a|b|c|d|e|f", await ((Second.IRightAccount)second).EchoAsync("a", "b", "c", "d", "e", "f"));
        var registry = provider.GetRequiredService<IActorRegistry>();
        Assert.Equal(new[] { "Calculator", "FirstAccount", "SecondAccount" },
            registry.Actors.Select(actor => actor.ActorType));
        Assert.True(registry.TryGet("FirstAccount", out var descriptor));
        Assert.Equal(typeof(First.AccountActor), descriptor.ImplementationType);
        Assert.False(registry.TryGet("Unknown", out _));

        var migrator = provider.GetRequiredService<IActorStateMigrator>();
        Assert.NotNull(migrator.ResolveFamily(typeof(CalculatorStateV2)));
        Assert.NotNull(migrator.ResolveFamily(typeof(AccountV2)));
        var serializer = provider.GetRequiredService<IActorWireSerializer>();
        var node = migrator.ResolveTargetNode(typeof(AccountV1))!;
        var bytes = await migrator.WriteEnvelopedAsync(
            new AccountV1 { Balance = 42, Owner = "owner", Count = 3, Revision = 9 }, node, serializer);
        await provider.GetRequiredService<IActorStateStore>().WriteAsync("FirstAccount", "first", "account", bytes);
        var first = proxies.Create<First.IAccount>(new ActorId("first"), "FirstAccount");
        var state = await first.ReadAsync();
        Assert.Equal(42, state.Balance);
        Assert.Equal("owner", state.Owner);
        Assert.Equal(3, state.Count);
        Assert.Equal(9, state.Revision);
        Assert.False(state.Frozen);

        var persisted = await provider.GetRequiredService<IActorStateStore>()
            .ReadAsync("FirstAccount", "first", "account");
        Assert.Equal(migrator.ResolveTargetNode(typeof(AccountV2))!.Index,
            serializer.DeserializeFromBytes<ActorStateEnvelope<AccountV2>>(persisted!.Value)!.Discriminator.ChainIndex);
        Assert.Throws<InvalidOperationException>(() => proxies.Create<IActor>(new ActorId("unknown"), "Unknown"));
    }
}
