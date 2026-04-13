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
using System.Net.Http;
using System.Net.Sockets;
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
}
