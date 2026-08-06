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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.Actors.Next.Analyzers.Test;

public sealed class ActorsNextCodeFixTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Scaffolds_generated_actor_client_contract_when_actor_has_no_interface()
    {
        const string source = """
            using System;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [DaprActor]
            public sealed class {|DAPR1421:CartActor|} : Actor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
            }
            """;
        const string expected = """
            using System;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
            }
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
            }

            """;

        var fixedText = await ApplyMarkedCodeFixAsync(source, ActorAnalyzerDiagnostics.MissingGeneratedActorClient, codeActionIndex: 0);

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(fixedText));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Decorates_existing_actor_interface_with_generate_actor_client()
    {
        const string source = """
            using System;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            public interface ICartActor : IActor
            {
            }

            [DaprActor]
            public sealed class {|DAPR1421:CartActor|} : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
            }
            """;
        const string fixedSource = """
            using System;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
            }

            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
            }
            """;

        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1421");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Inlines_task_run_lambda()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [GenerateActorClient]

            public interface ICartActor : IActor

            {

            }
            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public async Task TurnAsync()
                {
                    await {|DAPR1411:Task.Run(() => SaveAsync())|};
                }
                private static Task SaveAsync() => Task.CompletedTask;
            }
            """;
        const string fixedSource = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [GenerateActorClient]

            public interface ICartActor : IActor

            {

            }
            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public async Task TurnAsync()
                {
                    await SaveAsync();
                }
                private static Task SaveAsync() => Task.CompletedTask;
            }
            """;

        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1411");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Inlines_task_run_anonymous_delegate_return()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [GenerateActorClient]

            public interface ICartActor : IActor

            {

            }
            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public async Task TurnAsync()
                {
                    await {|DAPR1411:Task.Run(delegate { return SaveAsync(); })|};
                }
                private static Task SaveAsync() => Task.CompletedTask;
            }
            """;
        const string fixedSource = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [GenerateActorClient]

            public interface ICartActor : IActor

            {

            }
            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public async Task TurnAsync()
                {
                    await SaveAsync();
                }
                private static Task SaveAsync() => Task.CompletedTask;
            }
            """;

        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1411");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_inlines_task_run_simple_lambda()
    {
        const string source = "class C { void M() { var next = {|DAPR1411:Task.Run(value => Save(value))|}; } }";
        const string fixedSource = "class C { void M() { var next = Save(value); } }";

        var fixedText = await ApplyMarkedCodeFixAsync(source, ActorAnalyzerDiagnostics.SchedulerEscape, codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_preserves_non_lambda_task_run_argument()
    {
        const string source = "class C { void M() { var next = {|DAPR1411:Task.Run(Save())|}; } }";
        const string fixedSource = "class C { void M() { var next = Save(); } }";

        var fixedText = await ApplyMarkedCodeFixAsync(source, ActorAnalyzerDiagnostics.SchedulerEscape, codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_ignores_non_run_scheduler_escape()
    {
        const string source = "class C { void M() { {|DAPR1411:Task.Factory.StartNew(() => 1)|}; } }";
        const string fixedSource = "class C { void M() { Task.Factory.StartNew(() => 1); } }";

        var fixedText = await ApplyMarkedCodeFixAsync(source, ActorAnalyzerDiagnostics.SchedulerEscape, codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_result_to_await()
    {
        var source = ActorSource("_ = {|DAPR1412:Task.FromResult(1).Result|};");
        var fixedSource = ActorSource("_ = await Task.FromResult(1);");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1412");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_wait_to_await()
    {
        var source = ActorSource("{|DAPR1412:Task.Delay(1).Wait()|};");
        var fixedSource = ActorSource("await Task.Delay(1);");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1412");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_thread_sleep_to_task_delay()
    {
        var source = ActorSource("{|DAPR1412:Thread.Sleep(1)|};");
        var fixedSource = ActorSource("await Task.Delay(1);");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1412");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_ignores_unknown_blocking_node()
    {
        const string source = "class C { void M() { var task = {|DAPR1412:Task.Delay(1)|}; } }";
        const string fixedSource = "class C { void M() { var task = Task.Delay(1); } }";

        var fixedText = await ApplyMarkedCodeFixAsync(source, ActorAnalyzerDiagnostics.BlockingCall, codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_datetime_to_time_provider()
    {
        var source = ActorSource("_ = {|DAPR1413:DateTime.UtcNow|};");
        var fixedSource = ActorSource("_ = TimeProvider.System.GetUtcNow().UtcDateTime;");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1413");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_datetime_now_to_time_provider()
    {
        var source = ActorSource("_ = {|DAPR1413:DateTime.Now|};");
        var fixedSource = ActorSource("_ = TimeProvider.System.GetLocalNow().DateTime;");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1413");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_datetime_offset_utc_now_to_time_provider()
    {
        var source = ActorSource("_ = {|DAPR1413:DateTimeOffset.UtcNow|};");
        var fixedSource = ActorSource("_ = TimeProvider.System.GetUtcNow();");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1413");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_datetime_offset_to_time_provider()
    {
        var source = ActorSource("_ = {|DAPR1413:DateTimeOffset.Now|};");
        var fixedSource = ActorSource("_ = TimeProvider.System.GetLocalNow();");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1413");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Converts_stopwatch_start_to_time_provider_timestamp()
    {
        var source = ActorSource("_ = {|DAPR1413:Stopwatch.StartNew()|};", extraUsing: "using System.Diagnostics;");
        var fixedSource = ActorSource("_ = TimeProvider.System.GetTimestamp();", extraUsing: "using System.Diagnostics;");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1413");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_ignores_unknown_time_source()
    {
        const string source = "class C { void M() { var now = {|DAPR1413:DateTime.Today|}; } }";
        const string fixedSource = "class C { void M() { var now = DateTime.Today; } }";

        var fixedText = await ApplyMarkedCodeFixAsync(source, ActorAnalyzerDiagnostics.DirectTime, codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Seeds_random_source()
    {
        var source = ActorSource("_ = {|DAPR1414:new Random()|};");
        var fixedSource = ActorSource("_ = new Random(0);");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1414");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_ignores_unknown_nondeterministic_source()
    {
        const string source = "class C { void M() { var value = {|DAPR1414:RandomNumberGenerator.Create()|}; } }";
        const string fixedSource = "class C { void M() { var value = RandomNumberGenerator.Create(); } }";

        var fixedText = await ApplyMarkedCodeFixAsync(source, ActorAnalyzerDiagnostics.NondeterministicSource, codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Replaces_random_shared_with_seeded_random()
    {
        var source = ActorSource("_ = {|DAPR1414:Random.Shared|};");
        var fixedSource = ActorSource("_ = new Random(0);");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1414");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Replaces_guid_new_guid_with_deterministic_placeholder()
    {
        var source = ActorSource("_ = {|DAPR1414:Guid.NewGuid()|};");
        var fixedSource = ActorSource("_ = Guid.Empty;");
        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1414");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Scaffolds_missing_upcaster_hop()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.State;

            namespace Sample;

            public sealed class CartStateV3 { public string Name { get; set; } = ""; public int Count { get; set; } }
            public sealed class ShoppingCartV4 { public string Name { get; set; } = ""; public int Quantity { get; set; } }

            [GenerateActorClient]

            public interface ICartActor : IActor

            {

            }
            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override IActorStateAccessor State => throw new NotImplementedException();

                public async Task ReadAsync(CancellationToken cancellationToken)
                {
                    _ = await State.GetOrCreateAsync<ShoppingCartV4>("cart", () => new ShoppingCartV4(), cancellationToken);
                }
            }
            """;
        const string fixedSource = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.State;

            namespace Sample;

            public sealed class CartStateV3 { public string Name { get; set; } = ""; public int Count { get; set; } }
            public sealed class ShoppingCartV4 { public string Name { get; set; } = ""; public int Quantity { get; set; } }

            [GenerateActorClient]

            public interface ICartActor : IActor

            {

            }
            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override IActorStateAccessor State => throw new NotImplementedException();

                public async Task ReadAsync(CancellationToken cancellationToken)
                {
                    _ = await State.GetOrCreateAsync<ShoppingCartV4>("cart", () => new ShoppingCartV4(), cancellationToken);
                }
            }

            public sealed class CartStateV3ToShoppingCartV4Upcaster : Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<Sample.CartStateV3, Sample.ShoppingCartV4>
            {
                public ValueTask<Sample.ShoppingCartV4> UpcastAsync(Sample.CartStateV3 state, CancellationToken cancellationToken = default) =>
                    ValueTask.FromResult(new Sample.ShoppingCartV4
                    {
                        Name = state.Name,
                    });
            }

            """;

        var fixedText = await ApplyCodeFixAsync(
            source,
            Diagnostic.Create(
                ActorAnalyzerDiagnostics.BrokenUpcasterChain,
                Location.None,
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add("upcaster.from", "Sample.CartStateV3")
                    .Add("upcaster.to", "Sample.ShoppingCartV4")
                    .Add("upcaster.copiedMembers", "Name"),
                "ShoppingCartV4",
                "CartStateV3"),
            codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Replaces_filter_business_logic_with_handoff_comment()
    {
        const string source = """
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.Filters;

            public sealed class Guard : IActorTurnFilter
            {
                public async ValueTask InvokeAsync(ActorTurnContext context, ActorTurnDelegate next)
                {
                    {|DAPR1416:DoBusiness()|};
                    await next(context);
                }
                private static void DoBusiness() { }
            }
            """;
        const string fixedSource = """
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.Filters;

            public sealed class Guard : IActorTurnFilter
            {
                public async ValueTask InvokeAsync(ActorTurnContext context, ActorTurnDelegate next)
                {
                    await next(context);
                }
                private static void DoBusiness() { }
            }
            """;

        return AnalyzerTest.VerifyCodeFixAsync(source, fixedSource, "DAPR1416");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Promotes_state_baseline()
    {
        const string source = """
            namespace Sample;

            public sealed class {|DAPR1410:CartState|}
            {
                public string Name { get; set; } = "";
            }
            """;
        const string shipped = "state|Sample.CartState|v=1|P:Name=int";
        const string fixedBaseline = "state|Sample.CartState|v=1|P:Name=string";

        const string fixedSource = """
            namespace Sample;

            public sealed class CartState
            {
                public string Name { get; set; } = "";
            }
            """;

        return AnalyzerTest.VerifyCodeFixWithBaselineAsync(source, fixedSource, shipped, fixedBaseline, "DAPR1410", codeActionIndex: 0);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_ignores_baseline_promotion_without_required_properties()
    {
        const string source = "class CartState { }";
        var diagnostic = Diagnostic.Create(ActorAnalyzerDiagnostics.StateShapeChanged, Location.None, "CartState", "changed");

        var fixedText = await ApplyCodeFixAsync(source, diagnostic, codeActionIndex: 0);

        Assert.Equal(source, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Direct_code_fix_ignores_baseline_promotion_without_shipped_file()
    {
        const string source = "class CartState { }";
        var diagnostic = Diagnostic.Create(
            ActorAnalyzerDiagnostics.StateShapeChanged,
            Location.None,
            properties: ImmutableDictionary<string, string?>.Empty
                .Add("baseline.current", "state|CartState|v=1|")
                .Add("baseline.kind", "state")
                .Add("baseline.name", "CartState"),
            "CartState",
            "changed");

        var fixedText = await ApplyCodeFixAsync(source, diagnostic, codeActionIndex: 0);

        Assert.Equal(source, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Scaffolds_state_upcaster_for_state_baseline_change()
    {
        const string source = """
            namespace Sample;

            public sealed class CartState
            {
                public string Name { get; set; } = "";
            }
            """;
        const string fixedSource = """
            namespace Sample;

            public sealed class CartState
            {
                public string Name { get; set; } = "";
            }

            public sealed class CartStateUpcaster : Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<Sample.CartState, Sample.CartState>
            {
                public ValueTask<Sample.CartState> UpcastAsync(Sample.CartState state, CancellationToken cancellationToken = default) =>
                    throw new NotImplementedException();
            }

            """;

        var fixedText = await ApplyCodeFixAsync(
            source,
            Diagnostic.Create(
                ActorAnalyzerDiagnostics.StateShapeChanged,
                Location.None,
                properties: ImmutableDictionary<string, string?>.Empty.Add("baseline.name", "Sample.CartState"),
                "Sample.CartState",
                "member 'Name' changed"),
            codeActionIndex: 1);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Bumps_actor_contract_version_for_wire_break()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface {|DAPR1418:ICartActor|} : IActor
            {
                Task Save(string id);
            }

            [DaprActor(ContractVersion = 1)]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Save(string id) => Task.CompletedTask;
            }
            """;
        const string fixedSource = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task Save(string id);
            }

            [DaprActor(ContractVersion = 2)]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Save(string id) => Task.CompletedTask;
            }
            """;
        const string shipped = "interface|Sample.ICartActor|v=1|M:Save(int)=System.Threading.Tasks.Task";

        return AnalyzerTest.VerifyCodeFixWithBaselineAsync(source, fixedSource, shipped, shipped, "DAPR1418", codeActionIndex: 0);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Promotes_wire_baseline_for_contract_break()
    {
        const string source = """
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface {|DAPR1418:ICartActor|} : IActor
            {
                Task Save(string id);
            }

            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new System.NotImplementedException();
                public Task Save(string id) => Task.CompletedTask;
            }
            """;
        const string fixedSource = """
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task Save(string id);
            }

            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new System.NotImplementedException();
                public Task Save(string id) => Task.CompletedTask;
            }
            """;
        const string shipped = "interface|Sample.ICartActor|v=1|M:Save(int)=System.Threading.Tasks.Task";
        const string fixedBaseline = "interface|Sample.ICartActor|v=1|M:Save(string)=System.Threading.Tasks.Task";

        return AnalyzerTest.VerifyCodeFixWithBaselineAsync(source, fixedSource, shipped, fixedBaseline, "DAPR1418", codeActionIndex: 1);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Adds_actor_contract_version_for_wire_break()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface {|DAPR1418:ICartActor|} : IActor
            {
                Task Save(string id);
            }

            [DaprActor]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Save(string id) => Task.CompletedTask;
            }
            """;
        const string fixedSource = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task Save(string id);
            }

            [DaprActor(ContractVersion = 2)]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Save(string id) => Task.CompletedTask;
            }
            """;
        const string shipped = "interface|Sample.ICartActor|v=1|M:Save(int)=System.Threading.Tasks.Task";

        return AnalyzerTest.VerifyCodeFixWithBaselineAsync(source, fixedSource, shipped, shipped, "DAPR1418", codeActionIndex: 0);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Suggests_closest_matching_scheduled_callback_for_string_literal()
    {
        const string markedSource = """class C { void M() { var callback = {|DAPR1429:"AbandonCrt"|}; } }""";
        const string fixedSource = """class C { void M() { var callback = "AbandonCart"; } }""";

        var source = ExtractMarkup(markedSource, ActorAnalyzerDiagnostics.UnknownScheduledCallback.Id, out var span);
        var diagnostic = Diagnostic.Create(
            ActorAnalyzerDiagnostics.UnknownScheduledCallback,
            Location.Create("Test0.cs", span, new LinePositionSpan()),
            properties: ImmutableDictionary<string, string?>.Empty.Add("callback.candidates", "AbandonCart;GetSummary"),
            "Cart",
            "AbandonCrt");

        var fixedText = await ApplyCodeFixAsync(source, diagnostic, codeActionIndex: 0);

        Assert.Equal(fixedSource, fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Suggests_closest_matching_scheduled_callback_for_nameof()
    {
        const string markedSource = """class C { string AbandonCart() => null; void M() { var callback = {|DAPR1429:nameof(AbandonCart)|}; } }""";

        var source = ExtractMarkup(markedSource, ActorAnalyzerDiagnostics.UnknownScheduledCallback.Id, out var span);
        var diagnostic = Diagnostic.Create(
            ActorAnalyzerDiagnostics.UnknownScheduledCallback,
            Location.Create("Test0.cs", span, new LinePositionSpan()),
            properties: ImmutableDictionary<string, string?>.Empty.Add("callback.candidates", "AbandonCartNow"),
            "Cart",
            "AbandonCart");

        var fixedText = await ApplyCodeFixAsync(source, diagnostic, codeActionIndex: 0);

        Assert.Equal("""class C { string AbandonCart() => null; void M() { var callback = nameof(AbandonCartNow); } }""", fixedText);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Scheduled_callback_fix_is_skipped_when_no_candidate_is_close()
    {
        const string markedSource = """class C { void M() { var callback = {|DAPR1429:"xx"|}; } }""";

        var source = ExtractMarkup(markedSource, ActorAnalyzerDiagnostics.UnknownScheduledCallback.Id, out var span);
        var diagnostic = Diagnostic.Create(
            ActorAnalyzerDiagnostics.UnknownScheduledCallback,
            Location.Create("Test0.cs", span, new LinePositionSpan()),
            properties: ImmutableDictionary<string, string?>.Empty.Add("callback.candidates", "CompletelyDifferentMethodName"),
            "Cart",
            "xx");

        var provider = new ActorsNextCodeFixProvider();
        var actions = await RegisterCodeFixesAsync(provider, source, diagnostic);

        Assert.Empty(actions);
    }

    private const string ActorTemplateStart = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Dapr.Actors.Next.Abstractions;
        using Dapr.Actors.Next.Abstractions.Attributes;

        [GenerateActorClient]

        public interface ICartActor : IActor

        {

        }
        [DaprActor]
        public sealed class CartActor : Actor, ICartActor
        {
            protected override ActorId Id => ActorId.Create("a");
            protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
            public async Task TurnAsync()
            {
        """;

    private const string ActorTemplateEnd = """
            }
        }
        """;

    private static string ActorSource(string statement, string extraUsing = "") =>
        (string.IsNullOrWhiteSpace(extraUsing) ? ActorTemplateStart : ActorTemplateStart.Replace("using System.Threading.Tasks;\r\n", "using System.Threading.Tasks;\r\n" + extraUsing + "\r\n", StringComparison.Ordinal).Replace("using System.Threading.Tasks;\n", "using System.Threading.Tasks;\n" + extraUsing + "\n", StringComparison.Ordinal)) +
        "\n        " + statement + "\n" + ActorTemplateEnd;

    private static Task<string> ApplyMarkedCodeFixAsync(string markedSource, DiagnosticDescriptor descriptor, int codeActionIndex)
    {
        var source = ExtractMarkup(markedSource, descriptor.Id, out var span);
        var diagnostic = Diagnostic.Create(descriptor, Location.Create("Test0.cs", span, new LinePositionSpan()));
        return ApplyCodeFixAsync(source, diagnostic, codeActionIndex);
    }

    private static async Task<List<CodeAction>> RegisterCodeFixesAsync(ActorsNextCodeFixProvider provider, string source, Diagnostic diagnostic)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution
            .AddProject("TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dapr.Actors.Next.Abstractions.IActor).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute).Assembly.Location),
            });
        var document = project.AddDocument("Test0.cs", SourceText.From(source));
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            TestContext.Current.CancellationToken);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    private static async Task<string> ApplyCodeFixAsync(string source, Diagnostic diagnostic, int codeActionIndex)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution
            .AddProject("TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dapr.Actors.Next.Abstractions.IActor).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute).Assembly.Location),
            });
        var document = project.AddDocument("Test0.cs", SourceText.From(source));
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            TestContext.Current.CancellationToken);

        var provider = new ActorsNextCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        var operations = await actions[codeActionIndex].GetOperationsAsync(TestContext.Current.CancellationToken);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(document.Id) ?? throw new InvalidOperationException("The fixed document was not found.");
        var changedText = await changedDocument.GetTextAsync(TestContext.Current.CancellationToken);
        return changedText.ToString();
    }

    private static string ExtractMarkup(string markedSource, string diagnosticId, out TextSpan span)
    {
        var startMarker = "{|" + diagnosticId + ":";
        const string endMarker = "|}";
        var start = markedSource.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("The diagnostic start marker was not found.");
        }

        var contentStart = start + startMarker.Length;
        var end = markedSource.IndexOf(endMarker, contentStart, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException("The diagnostic end marker was not found.");
        }

        var before = markedSource.Substring(0, start);
        var marked = markedSource.Substring(contentStart, end - contentStart);
        var after = markedSource.Substring(end + endMarker.Length);
        span = TextSpan.FromBounds(before.Length, before.Length + marked.Length);
        return before + marked + after;
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
