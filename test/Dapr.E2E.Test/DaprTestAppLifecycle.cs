// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Dapr.E2E.Test
{
    public class DaprTestAppLifecycle : IClassFixture<DaprTestAppFixture>, IAsyncLifetime
    {

        private readonly ITestOutputHelper output;
        private readonly DaprTestAppFixture fixture;
        private DaprTestAppFixture.State state;

        public DaprTestAppLifecycle(ITestOutputHelper output, DaprTestAppFixture fixture)
        {
            this.output = output;
            this.fixture = fixture;
        }

        public DaprRunConfiguration Configuration { get; set; }

        public string AppId => this.state?.App.AppId;

        public string HttpEndpoint => this.state?.HttpEndpoint;

        public string GrpcEndpoint => this.state?.GrpcEndpoint;

        public async Task InitializeAsync()
        {
            this.state = await this.fixture.StartAsync(this.output, this.Configuration);

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
    }
}