// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Dapr.E2E.Test.Actors;
using Xunit;
using Xunit.Abstractions;

namespace Dapr.E2E.Test
{
    // We're using IClassFixture to manage the state we need across tests.
    //
    // So for that reason we need all of the E2E tests to be one big class.
    // We're using partials to organize the functionality.
    public partial class E2ETests : IClassFixture<DaprTestAppFixture>, IAsyncLifetime
    {
        private readonly Lazy<IActorProxyFactory> proxyFactory;
        private readonly DaprTestAppFixture fixture;
        private DaprTestAppFixture.State state;

        public E2ETests(ITestOutputHelper output, DaprTestAppFixture fixture)
        {
            this.Output = output;
            this.fixture = fixture;

            this.proxyFactory = new Lazy<IActorProxyFactory>(() =>
            {
                Debug.Assert(this.HttpEndpoint != null);
                return new ActorProxyFactory(new ActorProxyOptions(){ HttpEndpoint = this.HttpEndpoint, });
            });
        }

        protected ITestOutputHelper Output { get; }

        public string AppId => this.state?.App.AppId;

        public string HttpEndpoint => this.state?.HttpEndpoint;

        public string GrpcEndpoint => this.state?.GrpcEndpoint;

        public IActorProxyFactory ProxyFactory => this.HttpEndpoint == null ? null : this.proxyFactory.Value;

        public async Task InitializeAsync()
        {
            this.state = await this.fixture.StartAsync(this.Output);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var client = new HttpClient();

            while (!cts.IsCancellationRequested)
            {
                cts.Token.ThrowIfCancellationRequested();

                var response = await client.GetAsync($"{HttpEndpoint}/v1.0/healthz");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            throw new TimeoutException("Timed out waiting for daprd health check");
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        protected async Task WaitForActorRuntimeAsync(IPingActor proxy, CancellationToken cancellationToken)
        {
            while (true)
            {
                this.Output.WriteLine($"Waiting for actor to be ready in: {this.AppId}");
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await proxy.Ping();
                    this.Output.WriteLine($"Found actor in: {this.AppId}");
                    break;
                }
                catch (DaprApiException)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250));
                }
            }
        }
    }
}
