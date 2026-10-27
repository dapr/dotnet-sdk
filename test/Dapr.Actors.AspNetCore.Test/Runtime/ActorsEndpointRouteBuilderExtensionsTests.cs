// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dapr.Actors.Runtime;

public class ActorsEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public async Task MapActorsHandlers_MapDaprConfigEndpoint()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<TestActor>();
        });
        var server = host.GetTestServer();

        var httpClient = server.CreateClient();
        var response = await httpClient.GetAsync("/dapr/config", TestContext.Current.CancellationToken);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(@"{""entities"":[""TestActor""],""reentrancy"":{""enabled"":false}}", text);
    }

    [Fact]
    public async Task MapActorsHandlers_HealthzEndpointResponds()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<TestActor>();
        });
        var server = host.GetTestServer();
        var httpClient = server.CreateClient();

        var response = await httpClient.GetAsync("/healthz", TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx but got {response.StatusCode}");
    }

    [Fact]
    public async Task MapActorsHandlers_InvokeMethodRouteReturnsForRegisteredActor()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<RealMethodActor>();
        });
        var server = host.GetTestServer();
        var httpClient = server.CreateClient();

        // PUT /actors/{actorTypeName}/{actorId}/method/{methodName}
        var request = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Put,
            $"/actors/RealMethodActor/actor1/method/{nameof(IRealMethodActor.PingAsync)}");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Not a 404 — route was matched and the method was invoked.
        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx but got {response.StatusCode}");
    }

    [Fact]
    public async Task MapActorsHandlers_UnregisteredActorType_ThrowsInvalidOperationException()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<TestActor>();
        });
        var server = host.GetTestServer();
        var httpClient = server.CreateClient();

        // PUT /actors/{unknownType}/{id}/method/{method} — should throw because the type is not registered.
        var request = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Put,
            "/actors/DoesNotExist/id1/method/Foo");

        // The TestServer propagates the unhandled exception from the route handler.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            httpClient.SendAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MapActorsHandlers_SameActivationIdHeader_ReusesActivation()
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>());
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        var second = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");

        Assert.Equal(first, second);
        Assert.Equal(new[] { $"activate:{first}", $"call:{first}", $"call:{first}" }, ActivationActor.Events(actorId));
    }

    [Fact]
    public async Task MapActorsHandlers_NewActivationIdHeader_DeactivatesAndActivatesFresh()
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>());
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        var second = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a2");

        Assert.NotEqual(first, second);
        Assert.Equal(new[] { $"activate:{first}", $"call:{first}", $"deactivate:{first}", $"activate:{second}", $"call:{second}" }, ActivationActor.Events(actorId));
    }

    [Fact]
    public async Task MapActorsHandlers_ActivationIdHeaderName_IsCaseInsensitive()
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>());
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        var same = await InvokeWhoAmIAsync(httpClient, actorId, "dapr-actor-activation-id", "a1");
        var reset = await InvokeWhoAmIAsync(httpClient, actorId, "DAPR-ACTOR-ACTIVATION-ID", "a2");

        Assert.Equal(first, same);
        Assert.NotEqual(first, reset);
        Assert.Contains($"deactivate:{first}", ActivationActor.Events(actorId));
    }

    [Fact]
    public async Task MapActorsHandlers_MissingActivationIdHeader_NeverResets()
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>());
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        var second = await InvokeWhoAmIAsync(httpClient, actorId, null, null);
        var third = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "");

        Assert.Equal(first, second);
        Assert.Equal(first, third);
        Assert.DoesNotContain(ActivationActor.Events(actorId), e => e.StartsWith("deactivate:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MapActorsHandlers_ReminderWithNewActivationIdHeader_ResetsActivation()
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>());
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        await SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/remind/r1", "{\"dueTime\":\"0h0m1s0ms\"}", "dapr-actor-activation-id", "a1");
        await SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/remind/r1", "{\"dueTime\":\"0h0m1s0ms\"}", "dapr-actor-activation-id", "a2");
        var second = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a2");

        Assert.NotEqual(first, second);
        Assert.Equal(new[] { $"activate:{first}", $"call:{first}", $"reminder:{first}", $"deactivate:{first}", $"activate:{second}", $"reminder:{second}", $"call:{second}" }, ActivationActor.Events(actorId));
    }

    [Fact]
    public async Task MapActorsHandlers_TimerWithNewActivationIdHeader_ResetsActivation()
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>());
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        await SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/timer/t1", "{\"callback\":\"OnTimerAsync\"}", "Dapr-Actor-Activation-Id", "a1");
        await SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/timer/t1", "{\"callback\":\"OnTimerAsync\"}", "Dapr-Actor-Activation-Id", "a2");
        var second = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a2");

        Assert.NotEqual(first, second);
        Assert.Equal(new[] { $"activate:{first}", $"call:{first}", $"timer:{first}", $"deactivate:{first}", $"activate:{second}", $"timer:{second}", $"call:{second}" }, ActivationActor.Events(actorId));
    }

    [Fact]
    public async Task MapActorsHandlers_CallReplacedWhileRunning_Fails()
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>());
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        var gate = ActivationActor.Gate(actorId);
        var held = SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/{nameof(IActivationActor.HoldAsync)}", null, "Dapr-Actor-Activation-Id", "a1");
        await gate.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var replacing = InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a2");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!gate.Holder.Discarded)
        {
            await Task.Delay(10, timeout.Token);
        }

        Assert.False(gate.Deactivated.Task.IsCompleted);
        gate.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        await gate.Deactivated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var second = await replacing;
        Assert.NotEqual(first, second);
        Assert.Equal(second, await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a2"));
    }

    [Theory]
    [InlineData("1.19.0")]
    [InlineData("edge")]
    public async Task MapActorsHandlers_OlderSidecar_IgnoresActivationId(string runtimeVersion)
    {
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>(),
            getRuntimeVersion: _ => Task.FromResult(runtimeVersion));
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var first = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        await SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/remind/r1", "{\"dueTime\":\"0h0m1s0ms\"}", "Dapr-Actor-Activation-Id", "a2");
        await SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/timer/t1", "{\"callback\":\"OnTimerAsync\"}", "Dapr-Actor-Activation-Id", "a3");
        var second = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a4");

        Assert.Equal(first, second);
        Assert.DoesNotContain(ActivationActor.Events(actorId), e => e.StartsWith("deactivate:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MapActorsHandlers_FailedVersionLookup_IgnoresActivationIdThenRetries()
    {
        var lookups = 0;
        using var host = CreateHost<ActorsStartup>(options => options.Actors.RegisterActor<ActivationActor>(),
            getRuntimeVersion: _ => Interlocked.Increment(ref lookups) == 1
                ? Task.FromException<string>(new HttpRequestException("sidecar down"))
                : Task.FromResult("1.19.1"));
        var httpClient = host.GetTestServer().CreateClient();
        var actorId = Guid.NewGuid().ToString("N");

        var ignored = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        var trusted = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");
        var same = await InvokeWhoAmIAsync(httpClient, actorId, "Dapr-Actor-Activation-Id", "a1");

        Assert.NotEqual(ignored, trusted);
        Assert.Equal(trusted, same);
        Assert.Equal(2, lookups);
    }

    private static async Task<int> InvokeWhoAmIAsync(HttpClient httpClient, string actorId, string headerName, string activationId)
    {
        var body = await SendAsync(httpClient, $"/actors/ActivationActor/{actorId}/method/{nameof(IActivationActor.WhoAmIAsync)}", null, headerName, activationId);
        return int.Parse(body);
    }

    private static async Task<string> SendAsync(HttpClient httpClient, string path, string body, string headerName, string activationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        if (headerName is not null)
        {
            request.Headers.TryAddWithoutValidation(headerName, activationId);
        }

        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx but got {response.StatusCode}");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Reminder")]
    [InlineData("Timer")]
    public async Task MapActorsHandlers_CallbacksPropagateReentrancyContext(string callback)
    {
        var probe = new CallbackContextProbe();
        using var host = CreateHost<ActorsStartup>(
            options => options.Actors.RegisterActor<CallbackContextActor>(),
            services => services.AddSingleton(probe));
        using var client = host.GetTestServer().CreateClient();
        var path = callback == "Timer"
            ? "/actors/CallbackContextActor/id/method/timer/test"
            : "/actors/CallbackContextActor/id/method/remind/test";
        var body = callback == "Timer"
            ? "{\"callback\":\"TimerCallback\"}"
            : "{\"data\":\"\",\"dueTime\":\"0h0m0s0ms\",\"period\":\"0h0m1s0ms\"}";

        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        request.Headers.Add("Dapr-Reentrancy-Id", "callback-chain");
        request.Content = new StringContent(body);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("callback-chain", probe.Context);

        using var next = await client.PutAsync(path, new StringContent(body), TestContext.Current.CancellationToken);
        next.EnsureSuccessStatusCode();
        Assert.Null(probe.Context);
    }

    private static IHost CreateHost<TStartup>(Action<ActorRuntimeOptions> configure,
        Action<IServiceCollection> configureServices = null,
        Func<CancellationToken, Task<string>> getRuntimeVersion = null) where TStartup : class
    {
        getRuntimeVersion ??= _ => Task.FromResult("1.19.1");
        var builder = Host
            .CreateDefaultBuilder()
            .ConfigureLogging(b =>
            {
                // shhhh
                b.SetMinimumLevel(LogLevel.None);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<TStartup>();
                webBuilder.UseTestServer();
            })
            .ConfigureServices(services =>
            {
                services.AddActors(configure);
                services.AddSingleton(new ActorActivationIdGate(getRuntimeVersion));
                configureServices?.Invoke(services);
            });
        var host = builder.Build();
        try
        {
            host.Start();
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }

    private class ActorsStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddActors(default);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapActorsHandlers();
            });
        }
    }

    private interface ITestActor : IActor
    {
    }

    private class TestActor : Actor, ITestActor
    {
        public TestActor(ActorHost host) : base(host) { }
    }

    private interface IRealMethodActor : IActor
    {
        Task PingAsync();
    }

    private class RealMethodActor : Actor, IRealMethodActor
    {
        public RealMethodActor(ActorHost host) : base(host) { }
        public Task PingAsync() => Task.CompletedTask;
    }

    private interface IActivationActor : IActor
    {
        Task<int> WhoAmIAsync();

        Task HoldAsync();
    }

    private class ActivationActor : Actor, IActivationActor, IRemindable
    {
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> events = new();
        private static int counter;
        private readonly int instance = Interlocked.Increment(ref counter);

        public ActivationActor(ActorHost host) : base(host) { }

        private static readonly ConcurrentDictionary<string, HoldGate> gates = new();

        public static string[] Events(string actorId) => events.GetOrAdd(actorId, _ => new()).ToArray();

        public static HoldGate Gate(string actorId) => gates.GetOrAdd(actorId, _ => new());

        public async Task HoldAsync()
        {
            var gate = Gate(Id.GetId());
            gate.Holder = this;
            gate.Held.SetResult();
            await gate.Release.Task;
        }

        public Task<int> WhoAmIAsync()
        {
            Record("call");
            return Task.FromResult(instance);
        }

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            Record("reminder");
            return Task.CompletedTask;
        }

        public Task OnTimerAsync()
        {
            Record("timer");
            return Task.CompletedTask;
        }

        protected override Task OnActivateAsync()
        {
            Record("activate");
            return Task.CompletedTask;
        }

        protected override Task OnDeactivateAsync()
        {
            Record("deactivate");
            Gate(Id.GetId()).Deactivated.TrySetResult();
            return Task.CompletedTask;
        }

        private void Record(string kind) => events.GetOrAdd(Id.GetId(), _ => new()).Enqueue($"{kind}:{instance}");
    }

    private sealed class HoldGate
    {
        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Deactivated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Actor Holder { get; set; }
    }

    public sealed class CallbackContextProbe
    {
        public string Context { get; set; }
    }

    public sealed class CallbackContextActor(ActorHost host, CallbackContextProbe probe)
        : Actor(host), IRealMethodActor, IRemindable
    {
        public Task PingAsync() => Task.CompletedTask;

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            probe.Context = ActorReentrancyContextAccessor.ReentrancyContext;
            return Task.CompletedTask;
        }

        public Task TimerCallback()
        {
            probe.Context = ActorReentrancyContextAccessor.ReentrancyContext;
            return Task.CompletedTask;
        }
    }
}
