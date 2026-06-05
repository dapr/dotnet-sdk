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
    // WaitForHttpReachableAsync — used by DaprdContainer to eliminate the brief
    // "Connection refused" window after the TCP port first opens.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WaitForHttpReachableAsync_Returns_When2xxIsReceived()
    {
        using var httpClient = CreateMockClient(HttpStatusCode.NoContent); // 204

        await ContainerReadinessProbe.WaitForHttpReachableAsync(
            "http://127.0.0.1:9999/v1.0/healthz",
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken,
            httpClient);
    }

    [Fact]
    public async Task WaitForHttpReachableAsync_Returns_When5xxIsReceived()
    {
        // 500 / 503 mean "server is running but not yet healthy" — the reachability
        // check should return immediately rather than retrying.
        using var httpClient = CreateMockClient(HttpStatusCode.InternalServerError); // 500

        await ContainerReadinessProbe.WaitForHttpReachableAsync(
            "http://127.0.0.1:9999/v1.0/healthz",
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken,
            httpClient);
    }

    [Fact]
    public async Task WaitForHttpReachableAsync_Returns_WhenServerFirstRefusesThenResponds()
    {
        // First call throws (connection refused); second returns 500 (server now running).
        int callCount = 0;
        var handler = new DelegateHandler(async (_, ct) =>
        {
            callCount++;
            if (callCount == 1)
                throw new HttpRequestException("Simulated connection refused");
            // 500 is fine — server is up
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        using var httpClient = new HttpClient(handler);

        await ContainerReadinessProbe.WaitForHttpReachableAsync(
            "http://127.0.0.1:9999/v1.0/healthz",
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken,
            httpClient);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task WaitForHttpReachableAsync_ThrowsTimeoutException_WhenConnectionAlwaysRefused()
    {
        var handler = new DelegateHandler((_, _) =>
            throw new HttpRequestException("Simulated connection refused"));
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            ContainerReadinessProbe.WaitForHttpReachableAsync(
                "http://127.0.0.1:9999/v1.0/healthz",
                TimeSpan.FromMilliseconds(300),
                TestContext.Current.CancellationToken,
                httpClient));
    }

    [Fact]
    public async Task WaitForHttpReachableAsync_ThrowsOperationCanceledException_WhenTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new DelegateHandler((_, _) =>
            throw new HttpRequestException("Simulated connection refused"));
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ContainerReadinessProbe.WaitForHttpReachableAsync(
                "http://127.0.0.1:9999/v1.0/healthz",
                TimeSpan.FromSeconds(5),
                cts.Token,
                httpClient));
    }

    // ---------------------------------------------------------------------------
    // WaitForHttpHealthAsync — stricter check that requires a 2xx response.
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
    // WaitForGrpcServerReadyAsync — performs the HTTP/2 client-preface handshake
    // to confirm a gRPC server is actively serving (not just that TCP accepts).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WaitForGrpcServerReadyAsync_Returns_WhenServerSendsHttp2ServerPreface()
    {
        // Simulate an HTTP/2 server by replying with a minimal SETTINGS frame
        // (9-byte HTTP/2 frame header with type=0x4) as soon as we receive any bytes
        // from the client. A real gRPC server does the same on connection setup.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(TestContext.Current.CancellationToken);
            using var stream = new NetworkStream(socket, ownsSocket: false);

            // Wait for any client data, then send a SETTINGS frame (length=0, type=0x4).
            var buffer = new byte[1];
            _ = await stream.ReadAtLeastAsync(buffer, 1, throwOnEndOfStream: false, TestContext.Current.CancellationToken);

            // HTTP/2 SETTINGS frame: 24-bit length(0) | type(0x04) | flags(0) | stream(0)
            byte[] settingsFrame = [0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00];
            await stream.WriteAsync(settingsFrame, TestContext.Current.CancellationToken);
            await stream.FlushAsync(TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        try
        {
            await ContainerReadinessProbe.WaitForGrpcServerReadyAsync(
                "127.0.0.1", port, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            listener.Stop();
            try { await serverTask; } catch { /* fake server may be torn down mid-write */ }
        }
    }

    [Fact]
    public async Task WaitForGrpcServerReadyAsync_Times_Out_WhenServerAcceptsButNeverResponds()
    {
        // Simulate Docker port forwarding accepting TCP SYN before the upstream gRPC server
        // is wired up: we accept the connection and read the client's preface, but never
        // send any bytes back. The probe must time out rather than declaring readiness.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var serverCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = Task.Run(async () =>
        {
            try
            {
                while (!serverCts.IsCancellationRequested)
                {
                    using var socket = await listener.AcceptSocketAsync(serverCts.Token);
                    // Drain the preface and hold the socket open without replying.
                    var buffer = new byte[64];
                    try { _ = await socket.ReceiveAsync(buffer, serverCts.Token); } catch { }
                    await Task.Delay(TimeSpan.FromSeconds(5), serverCts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }, serverCts.Token);

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                ContainerReadinessProbe.WaitForGrpcServerReadyAsync(
                    "127.0.0.1", port, TimeSpan.FromMilliseconds(1500), TestContext.Current.CancellationToken));
        }
        finally
        {
            serverCts.Cancel();
            listener.Stop();
            try { await serverTask; } catch { }
        }
    }

    [Fact]
    public async Task WaitForGrpcServerReadyAsync_Times_Out_WhenPortIsRefused()
    {
        // Port is not listening at all — the probe should retry until the overall timeout.
        var port = PortUtilities.GetAvailablePort();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            ContainerReadinessProbe.WaitForGrpcServerReadyAsync(
                "127.0.0.1", port, TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitForGrpcServerReadyAsync_ThrowsOperationCanceledException_WhenTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var port = PortUtilities.GetAvailablePort();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ContainerReadinessProbe.WaitForGrpcServerReadyAsync(
                "127.0.0.1", port, TimeSpan.FromSeconds(5), cts.Token));
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
