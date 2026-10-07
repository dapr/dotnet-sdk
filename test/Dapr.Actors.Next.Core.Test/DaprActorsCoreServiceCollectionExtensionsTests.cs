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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Core.Transport;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Test;

public sealed class DaprActorsCoreServiceCollectionExtensionsTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_registers_generated_dapr_grpc_client_with_workflow_style_channel_options()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_GRPC_ENDPOINT"] = "http://127.0.0.1:51001",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDaprActorsCore(_ => { });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<P.Dapr.DaprClient>();
        var options = ApplyGrpcOptions(provider);

        Assert.NotNull(client);
        Assert.IsType<DaprActorInvocationClient>(provider.GetRequiredService<IActorInvocationClient>());
        Assert.IsType<DaprSidecarActorStateStore>(provider.GetRequiredService<IActorStateStore>());
        Assert.IsType<DaprSidecarActorTimerScheduler>(provider.GetRequiredService<IActorTimerScheduler>());
        Assert.IsType<DaprSidecarActorReminderScheduler>(provider.GetRequiredService<IActorReminderScheduler>());
        Assert.IsType<DaprActorEventsTransport>(provider.GetRequiredService<ISubscribeActorEventsTransport>());
        Assert.IsType<SocketsHttpHandler>(options.HttpHandler);
        var handler = (SocketsHttpHandler)options.HttpHandler!;
        Assert.Equal(Timeout.InfiniteTimeSpan, handler.ConnectTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, handler.PooledConnectionIdleTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, handler.PooledConnectionLifetime);
        Assert.Equal(TimeSpan.FromSeconds(60), handler.KeepAlivePingDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), handler.KeepAlivePingTimeout);
        Assert.Equal(HttpKeepAlivePingPolicy.Always, handler.KeepAlivePingPolicy);
        Assert.True(handler.EnableMultipleHttp2Connections);
        Assert.Null(options.MaxReceiveMessageSize);
        Assert.Null(options.MaxSendMessageSize);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_preserves_existing_generated_dapr_grpc_client()
    {
        var services = new ServiceCollection();
        var customClient = new TestDaprClient();
        services.AddSingleton<P.Dapr.DaprClient>(customClient);

        services.AddDaprActorsCore(_ => { });

        using var provider = services.BuildServiceProvider();

        Assert.Same(customClient, provider.GetRequiredService<P.Dapr.DaprClient>());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_merges_registrations_from_multiple_calls_into_one_registry()
    {
        var services = new ServiceCollection();
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add("Counter", typeof(ICounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
        });
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add("OtherCounter", typeof(IOtherCounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActorRuntimeRegistry>();

        Assert.Equal(
            new[] { "Counter", "OtherCounter" },
            registry.ActorTypes.OrderBy(static type => type, StringComparer.Ordinal).ToArray());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_merges_registrations_regardless_of_call_order()
    {
        foreach (var counterFirst in (bool[])[true, false])
        {
            var services = new ServiceCollection();
            if (counterFirst)
            {
                services.AddDaprActorsCore(static registrations =>
                    registrations.Add("Counter", typeof(ICounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher()));
                services.AddDaprActorsCore(static registrations =>
                    registrations.Add("OtherCounter", typeof(IOtherCounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher()));
            }
            else
            {
                services.AddDaprActorsCore(static registrations =>
                    registrations.Add("OtherCounter", typeof(IOtherCounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher()));
                services.AddDaprActorsCore(static registrations =>
                    registrations.Add("Counter", typeof(ICounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher()));
            }

            using var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<ActorRuntimeRegistry>();

            Assert.Equal(
                new[] { "Counter", "OtherCounter" },
                registry.ActorTypes.OrderBy(static type => type, StringComparer.Ordinal).ToArray());
        }
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task AddDaprActorsCore_dispatches_to_actor_type_registered_by_an_earlier_call()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new List<string>());
        services.AddSingleton<ScopedProbe>();
        services.AddInMemoryActorAdapters();
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add("Counter", typeof(ICounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
        });
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add("OtherCounter", typeof(IOtherCounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
        });

        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();

        var incremented = await runtime.InvokeAsync(
            "Counter", "merge-test", "Increment", System.Text.Encoding.UTF8.GetBytes("3"), new Dictionary<string, string>());

        Assert.Equal("3", System.Text.Encoding.UTF8.GetString(incremented!));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_registers_a_single_stream_manager_and_registry_across_calls()
    {
        var services = new ServiceCollection();
        services.AddDaprActorsCore(_ => { });
        services.AddDaprActorsCore(_ => { });

        Assert.Equal(1, services.Count(static descriptor => descriptor.ServiceType == typeof(SubscribeActorEventsStreamManager)));
        Assert.Equal(1, services.Count(static descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(SubscribeActorEventsStreamHostedService)));
        Assert.Equal(1, services.Count(static descriptor => descriptor.ServiceType == typeof(ActorRuntimeRegistry)));

        using var provider = services.BuildServiceProvider();
        var managers = provider.GetServices<IHostedService>().OfType<SubscribeActorEventsStreamHostedService>().ToList();

        Assert.Single(managers);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_starts_stream_manager_when_hosted_services_were_registered_first()
    {
        var services = new ServiceCollection();
        services.AddHostedService<PreexistingHostedService>();
        services.AddDaprActorsCore(_ => { });

        using var provider = services.BuildServiceProvider();

        var streamServices = provider.GetServices<IHostedService>().OfType<SubscribeActorEventsStreamHostedService>().ToList();
        Assert.Single(streamServices);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task AddDaprActorsCore_announces_actor_types_from_every_call_over_the_stream()
    {
        var harness = new InMemoryTransportHarness();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryActorAdapters();
        services.AddSingleton<ISubscribeActorEventsTransport>(harness);
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add("Counter", typeof(ICounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
        });
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add("OtherCounter", typeof(IOtherCounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
        });

        await using var provider = services.BuildServiceProvider();
        var service = provider.GetServices<IHostedService>().OfType<SubscribeActorEventsStreamHostedService>().Single();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(cts.Token);
        var first = await harness.WaitForStreamAsync(cts.Token);
        var second = await harness.WaitForStreamAsync(cts.Token);
        var firstAdvertisement = await first.ReceiveAsync(cts.Token);
        var secondAdvertisement = await second.ReceiveAsync(cts.Token);
        await service.StopAsync(cts.Token);

        Assert.Equal(SubscribeActorEventsFrameKind.RegisteredActors, firstAdvertisement.Kind);
        Assert.Equal(SubscribeActorEventsFrameKind.RegisteredActors, secondAdvertisement.Kind);

        var announced = new[]
        {
            System.Text.Encoding.UTF8.GetString(firstAdvertisement.Payload.Span),
            System.Text.Encoding.UTF8.GetString(secondAdvertisement.Payload.Span),
        };

        Assert.Contains("Counter", announced);
        Assert.Contains("OtherCounter", announced);
    }

    private static CounterActor CreateCounterActor(IServiceProvider serviceProvider, ActorId actorId) =>
        new(serviceProvider.GetRequiredService<ActorActivationContext>(),
            serviceProvider.GetRequiredService<IActorInvocationClient>(),
            serviceProvider.GetRequiredService<ScopedProbe>(),
            serviceProvider.GetRequiredService<List<string>>());

    private static GrpcChannelOptions ApplyGrpcOptions(ServiceProvider provider)
    {
        var channelOptions = new GrpcChannelOptions();
        var monitor = provider.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>();
        var clientType = typeof(P.Dapr.DaprClient);
        var optionCandidates = new[]
        {
            monitor.Get(clientType.FullName!),
            monitor.Get(clientType.Name),
        };

        foreach (var options in optionCandidates.Distinct())
        {
            foreach (var action in options.ChannelOptionsActions)
            {
                action(channelOptions);
            }
        }

        return channelOptions;
    }

    private sealed class TestDaprClient : P.Dapr.DaprClient
    {
    }

    private sealed class PreexistingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
