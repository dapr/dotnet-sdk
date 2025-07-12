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
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Dapr.E2E.Test.Actors;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Dapr.E2E.Test;

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

    public DaprRunConfiguration Configuration { get; set; } = new DaprRunConfiguration
    {
        UseAppPort = true,
        AppId = "testapp",
        TargetProject = "./../../../../../test/Dapr.E2E.Test.App/Dapr.E2E.Test.App.csproj"
    };

    public string AppId => this.state?.App.AppId;

    public string HttpEndpoint => this.state?.HttpEndpoint;

    public string GrpcEndpoint => this.state?.GrpcEndpoint;

    public IActorProxyFactory ProxyFactory => this.HttpEndpoint == null ? null : this.proxyFactory.Value;

    public async Task InitializeAsync()
    {
        this.state = await this.fixture.StartAsync(this.Output, this.Configuration);

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
        await ActorRuntimeChecker.WaitForActorRuntimeAsync(this.AppId, this.Output, proxy, cancellationToken);
    }
}