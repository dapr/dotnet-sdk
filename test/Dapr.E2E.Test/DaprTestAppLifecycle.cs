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
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Dapr.E2E.Test;

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

    public ITestOutputHelper Output => this.output;

    public async ValueTask InitializeAsync()
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
                // For gRPC apps, also wait for the app port to accept TCP connections before
                // returning so that the Dapr sidecar can successfully proxy gRPC requests.
                if (this.Configuration?.UseAppPort == true
                    && string.Equals(this.Configuration?.AppProtocol, "grpc", StringComparison.OrdinalIgnoreCase)
                    && (this.state.App?.AppPort ?? 0) > 0)
                {
                    await WaitForAppPortAsync(this.state.App.AppPort);
                }

                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);
        }

        throw new TimeoutException("Timed out waiting for daprd health check");
    }

    private static async Task WaitForAppPortAsync(int port)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!cts.IsCancellationRequested)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    await tcpClient.ConnectAsync(IPAddress.Loopback, port);
                    return;
                }
            }
            catch (SocketException)
            {
                // Port not yet accepting connections — retry after a short delay.
            }
            catch (Exception)
            {
                // Treat any other connection error as a transient failure and retry.
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException($"Timed out waiting for gRPC app to listen on port {port}");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
