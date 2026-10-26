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

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Actors.Next.SourceGenerators.Test;

public sealed partial class GeneratorTests
{
    [Fact]
    public void Aliased_attributes_and_partial_declarations_produce_identical_output()
    {
        const string source = """
                              using System.Threading;
                              using System.Threading.Tasks;
                              using Dapr.Actors.Next.Abstractions;
                              using Dapr.Actors.Next.Abstractions.State;
                              using Client = Dapr.Actors.Next.Abstractions.Attributes.GenerateActorClientAttribute;
                              using Host = Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute;
                              [Client] public partial interface IWallet : IActor
                              {
                                  Task<int> Read(int value = 42, CancellationToken cancellationToken = default);
                              }
                              [Host] public sealed partial class WalletActor : Actor, IWallet
                              {
                                  protected override ActorId Id => new("wallet");
                                  protected override IActorStateAccessor State => throw new System.NotImplementedException();
                                  public Task<int> Read(int value = 42, CancellationToken cancellationToken = default) => Task.FromResult(value);
                              }
                              public sealed partial class Wallet { public int Value { get; set; } }
                              public sealed partial class WalletV2 { public int Value { get; set; } public bool Frozen { get; set; } }
                              public sealed partial class Upgrade : IActorStateUpcaster<Wallet, WalletV2>
                              {
                                  public ValueTask<WalletV2> UpcastAsync(Wallet state, CancellationToken cancellationToken = default)
                                      => new(new WalletV2 { Value = state.Value });
                              }
                              """;
        var expected = RunGenerator(CreateCompilation("PartialDiscovery", source), false, true);
        var repeated = new StringBuilder(source);
        for (var i = 0; i < 64; i++)
        {
            repeated.AppendLine("""
                                public partial interface IWallet : IActor {}
                                public sealed partial class WalletActor : IWallet {}
                                public sealed partial class Wallet {}
                                public sealed partial class WalletV2 {}
                                public sealed partial class Upgrade : IActorStateUpcaster<Wallet, WalletV2> {}
                                """);
        }

        Assert.Equal(expected, RunGenerator(CreateCompilation("PartialDiscovery", repeated.ToString()), false, true));
        Assert.Contains("int value, global::System.Threading.CancellationToken cancellationToken = default", expected);
        Assert.Equal(1, CountOccurrences(expected, "class GeneratedWalletProxy"));
        Assert.Equal(1, CountOccurrences(expected, "class WalletActorDispatcher"));
        Assert.Contains("ActorStateMigrationEdge(0, 1, typeof(global::Upgrade))", expected);
    }

    [Fact]
    public void Reference_filtering_preserves_nested_actors_and_inherited_upcasters()
    {
        const string source = """
                              using System.Threading;
                              using System.Threading.Tasks;
                              using Dapr.Actors.Next.Abstractions;
                              using Dapr.Actors.Next.Abstractions.Attributes;
                              using Dapr.Actors.Next.Abstractions.State;
                              namespace ExternalDiscovery;
                              public static class Container
                              {
                                  [GenerateActorClient] public interface IRemote : IActor { Task<int> Read(); }
                                  [DaprActor("NestedRemote")] public sealed class Remote : Actor, IRemote
                                  {
                                      protected override ActorId Id => new("remote");
                                      protected override IActorStateAccessor State => throw new System.NotImplementedException();
                                      public Task<int> Read() => Task.FromResult(5);
                                  }
                                  public sealed class Legacy { public int Value { get; set; } }
                                  public sealed class Modern { public string Value { get; set; } = ""; }
                                  public abstract class UpgradeBase : IActorStateUpcaster<Legacy, Modern>
                                  {
                                      public ValueTask<Modern> UpcastAsync(Legacy state, CancellationToken cancellationToken = default)
                                          => new(new Modern { Value = state.Value.ToString() });
                                  }
                                  public sealed class Upgrade : UpgradeBase {}
                              }
                              """;
        var library = CreateCompilation("ExternalDiscovery", source);
        using var image = new MemoryStream();
        var emitted = library.Emit(image);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        var app = CreateCompilation("DiscoveryApp", "public class Marker {}",
            MetadataReference.CreateFromImage(image.ToArray()));

        Assert.Empty(RunGenerator(app, false, true));
        var generated = RunGenerator(app, true, true);
        Assert.Contains("class GeneratedRemoteProxy", generated);
        Assert.Contains("class RemoteDispatcher", generated);
        Assert.Contains("typeof(global::ExternalDiscovery.Container.Remote)", generated);
        Assert.Contains("ActorStateMigrationEdge(0, 1, typeof(global::ExternalDiscovery.Container.Upgrade))", generated);
        Assert.DoesNotContain("typeof(global::ExternalDiscovery.Container.UpgradeBase)", generated);
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("backward")]
    [InlineData("shortcut")]
    [InlineData("duplicate")]
    [InlineData("cycle")]
    public void Invalid_migration_graphs_keep_the_existing_diagnostic(string graph)
    {
        var source = """
                     public class Wallet { public int Value { get; set; } }
                     public class WalletV2 { public int Value { get; set; } }
                     public class WalletV3 { public int Value { get; set; } }
                     """;
        source += graph switch
        {
            "gap" => "",
            "backward" => UpcasterSource("Back", "WalletV2", "Wallet"),
            "shortcut" => UpcasterSource("Skip", "Wallet", "WalletV3"),
            "duplicate" => UpcasterSource("First", "Wallet", "WalletV2") + UpcasterSource("Second", "Wallet", "WalletV2"),
            "cycle" => UpcasterSource("Forward", "Wallet", "WalletV2") + UpcasterSource("Back", "WalletV2", "Wallet"),
            _ => throw new ArgumentOutOfRangeException(nameof(graph)),
        };
        if (graph == "gap")
        {
            source = source.Replace("public class WalletV2 {", "public class WalletV2 { public required string Extra { get; set; }");
        }

        var (generated, diagnostics) = RunGeneratorWithDiagnostics(CreateCompilation("InvalidGraphs", source), false, true);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("DAPR1422", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.DoesNotContain("new global::Dapr.Actors.Next.Abstractions.State.Versioning.ActorStateMigrationFamily(", generated);
    }

    [Theory]
    [InlineData("public int Value { get; set; }", "public int Value { get; set; } public bool Added { get; set; }", true)]
    [InlineData("public int Value { get; set; }", "public long Value { get; set; }", false)]
    [InlineData("public int Value { get; set; }", "public int Value { get; }", false)]
    [InlineData("public int Value { get; set; }", "public int Value { get; set; } public required string Added { get; set; }", false)]
    [InlineData("public int Value { get; set; }", "public WalletV2(int value) {} public int Value { get; set; }", false)]
    [InlineData("public int Value { get; set; }", "", false)]
    public void Lazy_member_discovery_preserves_additive_edge_rules(string from, string to, bool additive)
    {
        var source = $"public class Wallet {{ {from} }} public class WalletV2 {{ {to} }}";
        var (generated, diagnostics) = RunGeneratorWithDiagnostics(CreateCompilation("AdditiveRules", source), false, true);
        if (additive)
        {
            Assert.Empty(diagnostics);
            Assert.Contains("ActorStateMigrationEdge(0, 1, null)", generated);
            Assert.Contains("Value = source.Value", generated);
        }
        else
        {
            Assert.Equal("DAPR1422", Assert.Single(diagnostics).Id);
            Assert.Empty(generated);
        }
    }

    [Fact]
    public void Explicit_edges_do_not_require_writable_members_or_parameterless_constructors()
    {
        var source = """
                     public sealed class Legacy(int value) { public int Value { get; } = value; }
                     public sealed class Modern(string text) { public string Text { get; } = text; }
                     """;
        source += UpcasterSource("Upgrade", "Legacy", "Modern");
        var (generated, diagnostics) = RunGeneratorWithDiagnostics(CreateCompilation("ExplicitOnly", source), false, true);
        Assert.Empty(diagnostics);
        Assert.Contains("ActorStateMigrationEdge(0, 1, typeof(global::Upgrade))", generated);
        Assert.DoesNotContain("UpcastStateGenerated_", generated);
        Assert.Equal(2, Regex.Matches(generated, "h1:[0-9a-f]{32}").Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_manifest_merge_preserves_source_output_and_diagnostics(bool scanReferences)
    {
        const string source = """
                              public class Wallet { public int Value { get; set; } }
                              public class WalletV2 { public string Value { get; set; } = ""; }
                              """;
        // Use only framework references so the enabled scan also produces an empty reference manifest.
        var compilation = CreateCompilation("EmptyMerge", source);
        compilation = compilation.RemoveReferences(compilation.References.Where(reference =>
            Path.GetFileName(reference.Display)?.StartsWith("Dapr.", StringComparison.Ordinal) == true));
        var (generated, diagnostics) = RunGeneratorWithDiagnostics(compilation, scanReferences, true);
        Assert.Empty(generated);
        Assert.Equal("DAPR1422", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void Unrelated_wide_classes_do_not_allocate_member_models()
    {
        var source = new StringBuilder("public sealed class Unrelated {");
        for (var i = 0; i < 4096; i++)
        {
            source.AppendLine($"public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>> Field{i} = new();");
        }
        source.AppendLine("}");
        var compilation = CreateCompilation("WideUnrelated", source.ToString());
        RunGenerator(compilation, false, true);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Empty(RunGenerator(compilation, false));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 2 * 1024 * 1024, $"Unused member discovery allocated {allocated:N0} bytes.");
    }

    [Fact]
    public void Long_additive_chains_have_bounded_generator_allocations()
    {
        var source = new StringBuilder();
        for (var i = 0; i < 128; i++)
        {
            source.AppendLine($"public sealed class WalletV{i} {{ public int Value {{ get; set; }} }}");
        }
        var compilation = CreateCompilation("LongChain", source.ToString());
        RunGenerator(compilation, false, true);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var generated = RunGenerator(compilation, false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(127, CountOccurrences(generated, "new global::Dapr.Actors.Next.Abstractions.State.Versioning.ActorStateMigrationEdge("));
        Assert.Equal(128, Regex.Matches(generated, "h1:[0-9a-f]{32}").Count);
        Assert.True(allocated < 24 * 1024 * 1024, $"Chain generation allocated {allocated:N0} bytes.");
    }

    [Fact]
    public void Hashing_shared_member_graphs_does_not_allocate_unused_member_metadata()
    {
        var source = new StringBuilder("""
            namespace Dapr.Actors.Next.SourceGenerators.Test.ShapeHashAllocation;
            public class Wallet { public NodeL Graph { get; set; } }
            public class WalletV2 { public NodeL Graph { get; set; } public bool Added { get; set; } }
            public struct NodeA { public int Value; }
            """);
        for (var name = 'B'; name <= 'L'; name++)
        {
            source.AppendLine($"public struct Node{name} {{ public Node{(char)(name - 1)} Left; public Node{(char)(name - 1)} Right; }}");
        }
        var compilation = CreateCompilation("SharedHashGraph", source.ToString());
        RunGenerator(compilation, false, true);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var generated = RunGenerator(compilation, false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Current.TestOutputHelper!.WriteLine($"Hash graph generation allocated {allocated:N0} bytes.");
        Assert.Equal(2, Regex.Matches(generated, "h1:[0-9a-f]{32}").Count);
        Assert.True(allocated < 22 * 1024 * 1024, $"Hash graph generation allocated {allocated:N0} bytes.");
    }

    [Fact]
    public void Shape_hashes_preserve_h1_bytes_for_inheritance_recursion_arrays_generics_and_leaves()
    {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              public enum Status { Ready, Done }
                              public class Base { public int Hidden; public long Inherited { get; set; } }
                              public class Child { public int Value { get; set; } }
                              public class Box<T> { public T Value { get; set; } = default!; }
                              public class Wallet : Base
                              {
                                  public Child[] Array { get; set; } = [];
                                  public DateTime Date { get; set; }
                                  public DateTimeOffset DateOffset { get; set; }
                                  public decimal Decimal { get; set; }
                                  public TimeSpan Duration { get; set; }
                                  public Box<Child> Generic { get; set; } = new();
                                  public Guid Guid { get; set; }
                                  public new string Hidden = "";
                                  public Child Left { get; set; } = new();
                                  public List<Child> List { get; set; } = [];
                                  public Wallet? Next { get; set; }
                                  public Child Right { get; set; } = new();
                                  public Status Status { get; set; }
                                  public string this[int index] => "";
                                  public static int IgnoredStatic;
                                  private int IgnoredPrivate;
                              }
                              public class WalletV2 : Wallet { public bool Added { get; set; } }
                              """;
        var compilation = CreateCompilation("HashCompatibility", source);
        string Identity(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
            + ", " + type.ContainingAssembly!.Identity.GetDisplayName();
        var wallet = compilation.GetTypeByMetadataName("Wallet")!;
        var child = compilation.GetTypeByMetadataName("Child")!;
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        string Leaf(ITypeSymbol type) => $"type:{Identity(type)};";
        var childShape = Leaf(child) + $"member:Value:{Leaf(intType)};";
        string Members(bool recursiveWallet)
        {
            var builder = new StringBuilder();
            foreach (var name in new[] { "Array", "Date", "DateOffset", "Decimal", "Duration", "Generic", "Guid", "Hidden", "Inherited", "Left", "List", "Next", "Right", "Status" })
            {
                var member = (name == "Inherited" ? wallet.BaseType! : wallet).GetMembers(name).Single();
                var type = member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;
                var shape = name switch
                {
                    "Array" => $"array[{childShape}]",
                    "Left" or "Right" => childShape,
                    "Generic" or "List" => $"{Identity(((INamedTypeSymbol)type).ConstructedFrom)}<{childShape},>",
                    "Next" => Leaf(type) + (recursiveWallet ? "recursive;" : Members(true)),
                    _ => Leaf(type),
                };
                builder.Append($"member:{name}:{shape};");
            }
            return builder.ToString();
        }

        string Hash(string text) => "h1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 16)).ToLowerInvariant();
        var expected = new[]
        {
            Hash(Leaf(wallet) + Members(false)),
            Hash(Leaf(compilation.GetTypeByMetadataName("WalletV2")!)
                 + $"member:Added:{Leaf(compilation.GetSpecialType(SpecialType.System_Boolean))};" + Members(false)),
        };
        var generated = RunGenerator(compilation, false, true);
        Assert.Equal(expected, Regex.Matches(generated, "h1:[0-9a-f]{32}").Select(match => match.Value));
    }

    private static string UpcasterSource(string name, string from, string to) => $$"""
        public sealed class {{name}} : Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<{{from}}, {{to}}>
        {
            public System.Threading.Tasks.ValueTask<{{to}}> UpcastAsync({{from}} state, System.Threading.CancellationToken cancellationToken = default)
                => throw new System.NotImplementedException();
        }
        """;
}
