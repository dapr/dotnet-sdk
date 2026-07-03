using Microsoft.CodeAnalysis.Testing;

namespace Dapr.Actors.Next.Analyzers.Test;

public sealed class ActorsNextAnalyzerTests
{
    [Fact]
    public Task Actor_turn_escape_blocking_time_and_random_sources_are_reported()
    {
        const string source = """
            using System;
            using System.Diagnostics;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [DaprActor]
            public sealed class CartActor : Actor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();

                public async Task TurnAsync()
                {
                    await {|DAPR1411:Task.Run(() => Task.CompletedTask)|};
                    _ = {|DAPR1411:new Thread(() => { })|};
                    {|DAPR1411:ThreadPool.QueueUserWorkItem(_ => { })|};
                    {|DAPR1412:Task.Delay(1).Wait()|};
                    _ = {|DAPR1412:Task.FromResult(1).Result|};
                    {|DAPR1412:Thread.Sleep(1)|};
                    _ = {|DAPR1413:DateTime.UtcNow|};
                    _ = {|DAPR1413:Stopwatch.StartNew()|};
                    _ = {|DAPR1413:new Stopwatch()|};
                    _ = {|DAPR1414:Guid.NewGuid()|};
                    _ = {|DAPR1414:new Random()|};
                    _ = {|DAPR1414:Random.Shared|};
                }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Attribute_only_actor_implementation_is_checked()
    {
        const string source = """
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [DaprActor]
            public sealed class CartActor
            {
                public async Task TurnAsync()
                {
                    await {|DAPR1411:Task.Run(() => Task.CompletedTask)|};
                }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Non_actor_code_is_ignored_for_turn_determinism_rules()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;

            public sealed class Service
            {
                public async Task RunAsync()
                {
                    await Task.Run(() => Task.CompletedTask);
                    _ = DateTime.UtcNow;
                    _ = Guid.NewGuid();
                }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Actor_interface_return_types_are_validated()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                int {|DAPR1417:Count|}();
                Task Save();
                ValueTask<int> Load();
                IAsyncEnumerable<int> Stream();
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Duplicate_actor_type_names_for_shared_interface_are_reported()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Contracts
            {
                [GenerateActorClient]
                public interface ICartActor : IActor
                {
                    Task Save();
                }
            }

            namespace StoreA
            {
                using Contracts;

                [DaprActor]
                public sealed class {|DAPR1420:CartActor|} : Actor, ICartActor
                {
                    protected override ActorId Id => ActorId.Create("a");
                    protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                    public Task Save() => Task.CompletedTask;
                }
            }

            namespace StoreB
            {
                using Contracts;

                [DaprActor]
                public sealed class {|DAPR1420:CartActor|} : Actor, ICartActor
                {
                    protected override ActorId Id => ActorId.Create("b");
                    protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                    public Task Save() => Task.CompletedTask;
                }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Shared_interface_with_distinct_actor_attribute_names_is_silent()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task Save();
            }

            [DaprActor("StoreCart")]
            public sealed class StoreCartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Save() => Task.CompletedTask;
            }

            [DaprActor("WholesaleCart")]
            public sealed class WholesaleCartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("b");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Save() => Task.CompletedTask;
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Explicit_registration_alias_disambiguates_shared_interface_actor_names()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;
            using Dapr.Actors.Next.Abstractions.Options;

            namespace Contracts
            {
                [GenerateActorClient]
                public interface ICartActor : IActor
                {
                    Task Save();
                }
            }

            namespace StoreA
            {
                using Contracts;

                [DaprActor]
                public sealed class CartActor : Actor, ICartActor
                {
                    protected override ActorId Id => ActorId.Create("a");
                    protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                    public Task Save() => Task.CompletedTask;
                }
            }

            namespace StoreB
            {
                using Contracts;

                [DaprActor]
                public sealed class CartActor : Actor, ICartActor
                {
                    protected override ActorId Id => ActorId.Create("b");
                    protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                    public Task Save() => Task.CompletedTask;
                }
            }

            namespace Host
            {
                public static class Registration
                {
                    public static void Configure(DaprActorsOptions options)
                    {
                        options.Actors.RegisterActor<StoreB.CartActor>("StoreBCart");
                    }
                }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Mutable_actor_fields_are_reported_with_injected_client_allowlist()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            public sealed class PaymentClient { }

            [DaprActor]
            public sealed class CartActor : Actor
            {
                private List<string> items = new();
                private PaymentClient client = new();
                private readonly List<string> safe = new();
                private int count;
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(
            source,
            AnalyzerTest.Diagnostic("DAPR1419").WithSpan(11, 26, 11, 31));
    }

    [Fact]
    public Task Mutable_fields_outside_actor_implementations_are_ignored()
    {
        const string source = """
            using System.Collections.Generic;

            public sealed class Service
            {
                private List<string> items = new();
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Business_logic_inside_turn_filter_is_reported()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.Filters;

            public sealed class Guard : IActorTurnFilter
            {
                public async ValueTask InvokeAsync(ActorTurnContext context, ActorTurnDelegate next)
                {
                    DoBusiness();
                    await next(context);
                }

                private static void DoBusiness() { }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(
            source,
            AnalyzerTest.Info("DAPR1416").WithSpan(9, 9, 9, 21));
    }

    [Fact]
    public Task Expression_bodied_turn_filter_is_ignored()
    {
        const string source = """
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.Filters;

            public sealed class Guard : IActorTurnFilter
            {
                public ValueTask InvokeAsync(ActorTurnContext context, ActorTurnDelegate next) => next(context);
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Upcaster_version_gap_is_reported()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.State;

            namespace Sample;

            public sealed class CartStateV1 { }
            public sealed class CartStateV2 { }
            public sealed class CartStateV3 { }
            public sealed class CartStateV4 { }

            public sealed class CartStateV1ToV2 : IActorStateUpcaster<CartStateV1, CartStateV2>
            {
                public ValueTask<CartStateV2> UpcastAsync(CartStateV1 state, CancellationToken cancellationToken = default) => new(new CartStateV2());
            }

            public sealed class {|DAPR1415:CartStateV3ToV4|} : IActorStateUpcaster<CartStateV3, CartStateV4>
            {
                public ValueTask<CartStateV4> UpcastAsync(CartStateV3 state, CancellationToken cancellationToken = default) => new(new CartStateV4());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Complete_upcaster_chain_is_silent()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.State;

            namespace Sample;

            public sealed class CartStateV1 { }
            public sealed class CartStateV2 { }
            public sealed class CartStateV3 { }

            public sealed class CartStateV1ToV2 : IActorStateUpcaster<CartStateV1, CartStateV2>
            {
                public ValueTask<CartStateV2> UpcastAsync(CartStateV1 state, CancellationToken cancellationToken = default) => new(new CartStateV2());
            }

            public sealed class CartStateV2ToV3 : IActorStateUpcaster<CartStateV2, CartStateV3>
            {
                public ValueTask<CartStateV3> UpcastAsync(CartStateV2 state, CancellationToken cancellationToken = default) => new(new CartStateV3());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Non_versioned_upcaster_types_are_ignored()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions.State;

            public sealed class CartState { }
            public sealed class CartStateV2 { }

            public sealed class CartStateUpcaster : IActorStateUpcaster<CartState, CartStateV2>
            {
                public ValueTask<CartStateV2> UpcastAsync(CartState state, CancellationToken cancellationToken = default) => new(new CartStateV2());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task State_baseline_reports_breaking_change_but_allows_additive_change()
    {
        const string source = """
            namespace Sample;

            public sealed class CartState
            {
                public string Name { get; set; } = "";
                public string Count { get; set; } = "";
                public string Added { get; set; } = "";
            }
            """;
        const string shipped = "state|Sample.CartState|v=1|P:Name=string;P:Count=int";

        return AnalyzerTest.VerifyAnalyzerWithBaselineAsync(
            source,
            shipped,
            AnalyzerTest.Diagnostic("DAPR1410").WithSpan(3, 21, 3, 30));
    }

    [Fact]
    public Task Missing_state_baseline_is_silent()
    {
        const string source = """
            namespace Sample;

            public sealed class CartState
            {
                public string Name { get; set; } = "";
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task Wire_baseline_reports_in_place_break()
    {
        const string source = """
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            namespace Sample;

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task Save(string id);
            }
            """;
        const string shipped = "interface|Sample.ICartActor|v=1|M:Save(int)=System.Threading.Tasks.Task";

        return AnalyzerTest.VerifyAnalyzerWithBaselineAsync(
            source,
            shipped,
            AnalyzerTest.Diagnostic("DAPR1418").WithSpan(8, 18, 8, 28));
    }

    [Fact]
    public Task Wire_baseline_is_silent_when_contract_version_is_bumped_after_other_attributes()
    {
        const string source = """
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

            [Obsolete]
            [DaprActor(ContractVersion = 2)]
            public sealed class CartActor : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Save(string id) => Task.CompletedTask;
            }
            """;
        const string shipped = "interface|Sample.ICartActor|v=1|M:Save(int)=System.Threading.Tasks.Task";

        return AnalyzerTest.VerifyAnalyzerWithBaselineAsync(source, shipped);
    }
}
