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
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Testcontainers.Common;

/// <summary>
/// Provides methods to poll for container readiness conditions such as TCP port
/// reachability and HTTP health endpoint availability.
/// </summary>
internal static class ContainerReadinessProbe
{
    /// <summary>
    /// Polls the given TCP host/port until a connection can be established or the
    /// timeout elapses.
    /// </summary>
    /// <param name="host">The host to connect to (e.g. "127.0.0.1").</param>
    /// <param name="port">The TCP port to connect to.</param>
    /// <param name="timeout">Maximum time to wait before throwing <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">Token used to cancel waiting.</param>
    /// <exception cref="TimeoutException">Thrown when the port does not become reachable within <paramref name="timeout"/>.</exception>
    internal static async Task WaitForTcpPortAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);

                var completed = await Task.WhenAny(connectTask,
                    Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken));
                if (completed == connectTask)
                {
                    // Will throw if the connection failed
                    await connectTask;
                    return;
                }
            }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for TCP port {host}:{port} to accept connections.", lastError);
    }

    /// <summary>
    /// Polls the given HTTP <paramref name="url"/> until the HTTP server sends <em>any</em>
    /// response — including error responses such as 5xx — or the timeout elapses. Only retries
    /// when the underlying TCP connection is refused or the per-attempt timeout fires, meaning
    /// the HTTP server is not yet listening.
    /// </summary>
    /// <remarks>
    /// Use this method when you need to verify that an HTTP server has started and is processing
    /// requests without caring about application-level health status. For Dapr specifically,
    /// <c>/v1.0/healthz</c> may return 500 while Dapr is still initializing components or while
    /// a connected app has not yet started, but the server is already accepting and routing
    /// requests. A single successful HTTP round-trip (regardless of status code) guarantees that
    /// the HTTP and gRPC servers are both active, which eliminates the transient
    /// "Connection refused" window that can occur immediately after the TCP port first opens.
    /// </remarks>
    /// <param name="url">The URL to GET, e.g. "http://127.0.0.1:3500/v1.0/healthz".</param>
    /// <param name="timeout">Maximum total time to wait before throwing <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">Token used to cancel waiting.</param>
    /// <param name="httpClient">
    /// Optional <see cref="HttpClient"/> to use. When <c>null</c> a new instance is created and
    /// disposed automatically. Supply a custom instance for testing purposes.
    /// </param>
    /// <exception cref="TimeoutException">Thrown when no HTTP response is received within <paramref name="timeout"/>.</exception>
    internal static async Task WaitForHttpReachableAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        HttpClient? httpClient = null)
    {
        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient();

        try
        {
            var start = DateTimeOffset.UtcNow;
            Exception? lastError = null;

            while (DateTimeOffset.UtcNow - start < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Bound each individual attempt so a stalled connection does not exhaust the overall timeout.
                    using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    requestCts.CancelAfter(TimeSpan.FromSeconds(5));

                    // Any HTTP response (including 5xx) means the server is accepting connections
                    // and actively processing requests.
                    await httpClient.GetAsync(url, requestCts.Token);
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    lastError = ex;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            throw new TimeoutException(
                $"Timed out waiting for HTTP server at {url} to start accepting connections.", lastError);
        }
        finally
        {
            if (ownsClient)
                httpClient.Dispose();
        }
    }

    /// <summary>
    /// Polls the given HTTP <paramref name="url"/> until a 2xx response is received or the
    /// timeout elapses. Each individual HTTP attempt is bounded by a 5-second timeout to
    /// avoid stalling when the endpoint is not yet accepting connections.
    /// </summary>
    /// <param name="url">The URL to GET, e.g. "http://127.0.0.1:3500/v1.0/healthz".</param>
    /// <param name="timeout">Maximum total time to wait before throwing <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">Token used to cancel waiting.</param>
    /// <param name="httpClient">
    /// Optional <see cref="HttpClient"/> to use. When <c>null</c> a new instance is created and
    /// disposed automatically. Supply a custom instance for testing purposes.
    /// </param>
    /// <exception cref="TimeoutException">Thrown when the endpoint does not return a 2xx response within <paramref name="timeout"/>.</exception>
    internal static async Task WaitForHttpHealthAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        HttpClient? httpClient = null)
    {
        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient();

        try
        {
            var start = DateTimeOffset.UtcNow;
            Exception? lastError = null;

            while (DateTimeOffset.UtcNow - start < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Bound each individual attempt so a stalled connection does not exhaust the overall timeout.
                    using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    requestCts.CancelAfter(TimeSpan.FromSeconds(5));

                    var response = await httpClient.GetAsync(url, requestCts.Token);
                    var statusCode = (int)response.StatusCode;
                    if (statusCode >= 200 && statusCode < 300)
                    {
                        return;
                    }

                    lastError = new HttpRequestException($"Health endpoint at {url} returned HTTP {statusCode}.");
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    lastError = ex;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            throw new TimeoutException(
                $"Timed out waiting for health endpoint {url} to return a successful response.", lastError);
        }
        finally
        {
            if (ownsClient)
                httpClient.Dispose();
        }
    }

    // The 24-byte HTTP/2 connection preface that every HTTP/2 client must send
    // immediately after establishing the TCP connection. An HTTP/2 server replies
    // with at least a SETTINGS frame (minimum 9-byte frame header).
    // See RFC 7540 §3.5.
    private static readonly byte[] Http2ClientPreface =
        Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

    /// <summary>
    /// Polls the given TCP host/port until a gRPC (HTTP/2) server responds to the HTTP/2
    /// connection preface, or the timeout elapses.
    /// </summary>
    /// <remarks>
    /// A successful TCP connect (as performed by <see cref="WaitForTcpPortAsync"/>) only
    /// proves that the kernel/Docker port forwarding accepts SYN packets — it does not
    /// prove that an HTTP/2 (gRPC) server is actually wired up on the upstream socket. On
    /// slow CI hosts there can be a brief window in which Docker's published port
    /// forwarding accepts connections before the upstream gRPC server is fully serving,
    /// surfacing to the test as a transient
    /// <c>Grpc.Core.RpcException(StatusCode=Unavailable, "Error connecting to subchannel /
    /// Connection refused")</c> on the first RPC.
    /// <para>
    /// This probe defeats that race by performing the HTTP/2 connection handshake itself:
    /// it opens a TCP connection, sends the 24-byte HTTP/2 client preface, and waits for
    /// any bytes back from the server. Any HTTP/2-speaking server (including gRPC) will
    /// reply with a SETTINGS frame as soon as it is actively serving the connection. If the
    /// connection is accepted but no bytes arrive within the per-attempt timeout, the
    /// upstream is not yet ready and the probe retries.
    /// </para>
    /// </remarks>
    /// <param name="host">The host to connect to (e.g. "127.0.0.1").</param>
    /// <param name="port">The gRPC port to probe.</param>
    /// <param name="timeout">Maximum total time to wait before throwing <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">Token used to cancel waiting.</param>
    /// <exception cref="TimeoutException">Thrown when no HTTP/2 response is received within <paramref name="timeout"/>.</exception>
    internal static async Task WaitForGrpcServerReadyAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TcpClient? client = null;
            try
            {
                client = new TcpClient();

                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                // Bound each individual attempt so a stalled connection does not exhaust the overall timeout.
                attemptCts.CancelAfter(TimeSpan.FromSeconds(2));

                await client.ConnectAsync(host, port, attemptCts.Token);

                var stream = client.GetStream();

                // Send the HTTP/2 client preface; the server should reply with a SETTINGS frame.
                await stream.WriteAsync(Http2ClientPreface, attemptCts.Token);
                await stream.FlushAsync(attemptCts.Token);

                // Reading any byte back is sufficient to prove the server is speaking HTTP/2.
                // A full SETTINGS frame is 9 bytes of header + payload; we only need to see
                // that the server has begun responding.
                var buffer = new byte[1];
                var bytesRead = await stream.ReadAsync(buffer, attemptCts.Token);
                if (bytesRead > 0)
                {
                    return;
                }

                // EOF: the server accepted the connection but closed without sending the
                // server preface — it is not yet serving HTTP/2. Retry.
                lastError = new IOException("gRPC server closed the connection before sending the HTTP/2 server preface.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is SocketException
                    or IOException
                    or InvalidOperationException
                    or OperationCanceledException)
            {
                lastError = ex;
            }
            finally
            {
                client?.Dispose();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        throw new TimeoutException(
            $"Timed out waiting for gRPC server at {host}:{port} to respond to the HTTP/2 client preface.",
            lastError);
    }
}
