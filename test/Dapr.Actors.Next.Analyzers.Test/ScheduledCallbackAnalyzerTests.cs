namespace Dapr.Actors.Next.Analyzers.Test;

public sealed class ScheduledCallbackAnalyzerTests
{
    // Shared preamble: file-level usings plus stub scheduler interfaces declared under the real
    // Dapr.Actors.Next.Core.Timers namespace. The analyzer matches the schedulers by display name, so these
    // stubs stand in for the Core assembly the analyzer test project does not reference.
    private const string Preamble = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Dapr.Actors.Next.Abstractions;
        using Dapr.Actors.Next.Abstractions.Attributes;
        using Dapr.Actors.Next.Core.Timers;

        namespace Dapr.Actors.Next.Core.Timers
        {
            public interface IActorReminderScheduler
            {
                ValueTask ScheduleAsync(string actorType, ActorId actorId, string name, TimeSpan dueTime, TimeSpan period, byte[] arguments, TimeSpan? ttl = null, bool? overwrite = null, CancellationToken cancellationToken = default);
            }

            public interface IActorTimerScheduler
            {
                ValueTask ScheduleAsync(string actorType, ActorId actorId, string name, TimeSpan dueTime, string operationName, byte[] arguments, TimeSpan? period = null, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
                ValueTask RescheduleAsync(string actorType, ActorId actorId, string name, TimeSpan dueTime, string operationName, byte[] arguments, TimeSpan? period = null, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
            }
        }


        """;

    [MinimumDaprRuntimeFact("1.18")]
    public Task Reminder_with_unknown_callback_name_is_reported()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task AbandonCart(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorReminderScheduler reminders) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task AbandonCart(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await reminders.ScheduleAsync("Cart", Id, {|DAPR1429:"AbandonCrt"|}, TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Reminder_with_valid_callback_name_is_silent()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task AbandonCart(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorReminderScheduler reminders) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task AbandonCart(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule()
                {
                    await reminders.ScheduleAsync("Cart", Id, "AbandonCart", TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
                    await reminders.ScheduleAsync("Cart", Id, nameof(AbandonCart), TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
                }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Timer_with_unknown_operation_name_is_reported_and_registration_name_is_not_validated()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task RefreshPrices(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorTimerScheduler timers) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task RefreshPrices(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await timers.ScheduleAsync("Cart", Id, "not-a-method-name", TimeSpan.Zero, {|DAPR1429:"RefreshPrced"|}, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Timer_with_valid_operation_name_is_silent()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task RefreshPrices(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorTimerScheduler timers) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task RefreshPrices(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await timers.ScheduleAsync("Cart", Id, "refresh", TimeSpan.Zero, nameof(RefreshPrices), Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Timer_reschedule_with_unknown_operation_name_is_reported()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task RefreshPrices(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorTimerScheduler timers) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task RefreshPrices(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await timers.RescheduleAsync("Cart", Id, "refresh", TimeSpan.Zero, {|DAPR1429:"Nope"|}, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Callback_that_is_a_class_method_but_not_on_a_client_interface_is_reported()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task Ping(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorReminderScheduler reminders) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Ping(CancellationToken ct = default) => Task.CompletedTask;
                public Task AbandonCart(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await reminders.ScheduleAsync("Cart", Id, {|DAPR1431:"AbandonCart"|}, TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Callback_on_actor_interface_without_generate_client_attribute_is_reported()
    {
        var source = Preamble + """
            public interface IExtra : IActor
            {
                Task Extra(CancellationToken ct = default);
            }

            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task Ping(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorTimerScheduler timers) : Actor, ICartActor, IExtra
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task Ping(CancellationToken ct = default) => Task.CompletedTask;
                public Task Extra(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await timers.ScheduleAsync("Cart", Id, "timer", TimeSpan.Zero, {|DAPR1431:nameof(Extra)|}, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Unknown_actor_type_is_reported_as_unresolved()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task AbandonCart(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorReminderScheduler reminders) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task AbandonCart(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await reminders.ScheduleAsync("Warehouse", Id, {|DAPR1430:"DoSomething"|}, TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Clr_type_name_does_not_resolve_a_custom_dapr_actor_name()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task AbandonCart(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorReminderScheduler reminders) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task AbandonCart(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule() =>
                    await reminders.ScheduleAsync("CartActor", Id, {|DAPR1430:"AbandonCart"|}, TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Non_constant_actor_type_or_callback_is_not_validated()
    {
        var source = Preamble + """
            [GenerateActorClient]
            public interface ICartActor : IActor
            {
                Task AbandonCart(CancellationToken ct = default);
            }

            [DaprActor("Cart")]
            public sealed class CartActor(IActorReminderScheduler reminders) : Actor, ICartActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new NotImplementedException();
                public Task AbandonCart(CancellationToken ct = default) => Task.CompletedTask;

                public async Task Schedule(string dynamicName, string dynamicType)
                {
                    await reminders.ScheduleAsync("Cart", Id, dynamicName, TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
                    await reminders.ScheduleAsync(dynamicType, Id, "AbandonCrt", TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
                }
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Ambiguous_actor_type_name_is_left_to_the_duplicate_name_rule()
    {
        var source = Preamble + """
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

            public sealed class Scheduler(IActorReminderScheduler reminders)
            {
                public async Task Schedule() =>
                    await reminders.ScheduleAsync("CartActor", ActorId.Create("x"), "Save", TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerAsync(source);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public Task Actor_type_defined_in_a_referenced_assembly_is_resolved_and_validated()
    {
        const string library = """
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.Actors.Next.Abstractions;
            using Dapr.Actors.Next.Abstractions.Attributes;

            [GenerateActorClient]
            public interface IRemoteActor : IActor
            {
                Task DoWork(CancellationToken ct = default);
            }

            [DaprActor("Remote")]
            public sealed class RemoteActor : Actor, IRemoteActor
            {
                protected override ActorId Id => ActorId.Create("a");
                protected override Dapr.Actors.Next.Abstractions.State.IActorStateAccessor State => throw new System.NotImplementedException();
                public Task DoWork(CancellationToken ct = default) => Task.CompletedTask;
            }
            """;

        var source = Preamble + """
            public sealed class Scheduler(IActorReminderScheduler reminders)
            {
                public async Task Schedule() =>
                    await reminders.ScheduleAsync("Remote", ActorId.Create("x"), {|DAPR1429:"DoWrk"|}, TimeSpan.Zero, TimeSpan.Zero, Array.Empty<byte>());
            }
            """;

        return AnalyzerTest.VerifyAnalyzerWithActorLibraryAsync(source, library);
    }
}
