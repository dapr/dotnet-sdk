// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common;

namespace Dapr.Testcontainers.Test.Common;

public sealed class ContainerReadinessProbeTests
{
    // ---------------------------------------------------------------------------
    // WaitForTcpPortAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WaitForTcpPortAsync_Returns_WhenPortIsListening()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            // Should complete without throwing
            await ContainerReadinessProbe.WaitForTcpPortAsync(
                "127.0.0.1", port, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task WaitForTcpPortAsync_Retries_UntilPortIsListening()
    {
        // Start listener slightly after we begin probing to verify that retries happen
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop(); // stop immediately; we'll restart it after a delay

        var probeTask = ContainerReadinessProbe.WaitForTcpPortAsync(
            "127.0.0.1", port, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken); // let a couple of probe attempts fail
        var listener2 = new TcpListener(IPAddress.Loopback, port);
        listener2.Start();

        try
        {
            await probeTask; // should succeed now that the port is open
        }
        finally
        {
            listener2.Stop();
        }
    }

    [Fact]
    public async Task WaitForTcpPortAsync_ThrowsTimeoutException_WhenPortNeverListens()
    {
        // GetAvailablePort returns a port that is currently free (not listening)
        var port = PortUtilities.GetAvailablePort();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            ContainerReadinessProbe.WaitForTcpPortAsync(
                "127.0.0.1", port, TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitForTcpPortAsync_ThrowsOperationCanceledException_WhenTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var port = PortUtilities.GetAvailablePort();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ContainerReadinessProbe.WaitForTcpPortAsync(
                "127.0.0.1", port, TimeSpan.FromSeconds(5), cts.Token));
    }

    // ---------------------------------------------------------------------------
    // WaitForHttpHealthAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WaitForHttpHealthAsync_Returns_When2xxIsReceived()
    {
        using var httpClient = CreateMockClient(HttpStatusCode.NoContent); // 204

        await ContainerReadinessProbe.WaitForHttpHealthAsync(
            "http://127.0.0.1:9999/v1.0/healthz",
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken,
            httpClient);
    }

    [Fact]
    public async Task WaitForHttpHealthAsync_Returns_When200IsReceived()
    {
        using var httpClient = CreateMockClient(HttpStatusCode.OK); // 200

        await ContainerReadinessProbe.WaitForHttpHealthAsync(
            "http://127.0.0.1:9999/v1.0/healthz",
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken,
            httpClient);
    }

    [Fact]
    public async Task WaitForHttpHealthAsync_Retries_UntilSuccessful()
    {
        // First two calls return 503, third call returns 204
        using var httpClient = CreateMockClientWithFailures(HttpStatusCode.NoContent, failCount: 2);

        await ContainerReadinessProbe.WaitForHttpHealthAsync(
            "http://127.0.0.1:9999/v1.0/healthz",
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken,
            httpClient);
    }

    [Fact]
    public async Task WaitForHttpHealthAsync_ThrowsTimeoutException_WhenEndpointNeverSucceeds()
    {
        using var httpClient = CreateMockClient(HttpStatusCode.ServiceUnavailable); // 503 forever

        await Assert.ThrowsAsync<TimeoutException>(() =>
            ContainerReadinessProbe.WaitForHttpHealthAsync(
                "http://127.0.0.1:9999/v1.0/healthz",
                TimeSpan.FromMilliseconds(300),
                TestContext.Current.CancellationToken,
                httpClient));
    }

    [Fact]
    public async Task WaitForHttpHealthAsync_ThrowsOperationCanceledException_WhenTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var httpClient = CreateMockClient(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ContainerReadinessProbe.WaitForHttpHealthAsync(
                "http://127.0.0.1:9999/v1.0/healthz",
                TimeSpan.FromSeconds(5),
                cts.Token,
                httpClient));
    }

    [Fact]
    public async Task WaitForHttpHealthAsync_Retries_WhenHttpRequestExceptionIsThrown()
    {
        // First call throws HttpRequestException, second call returns 204
        int callCount = 0;
        var handler = new DelegateHandler(async (_, ct) =>
        {
            callCount++;
            if (callCount == 1)
                throw new HttpRequestException("Simulated connection refused");
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var httpClient = new HttpClient(handler);

        await ContainerReadinessProbe.WaitForHttpHealthAsync(
            "http://127.0.0.1:9999/v1.0/healthz",
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken,
            httpClient);

        Assert.Equal(2, callCount);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static HttpClient CreateMockClient(HttpStatusCode statusCode)
    {
        var handler = new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        return new HttpClient(handler);
    }

    private static HttpClient CreateMockClientWithFailures(HttpStatusCode successCode, int failCount)
    {
        var callCount = 0;
        var handler = new DelegateHandler((_, _) =>
        {
            callCount++;
            var code = callCount <= failCount ? HttpStatusCode.ServiceUnavailable : successCode;
            return Task.FromResult(new HttpResponseMessage(code));
        });
        return new HttpClient(handler);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
