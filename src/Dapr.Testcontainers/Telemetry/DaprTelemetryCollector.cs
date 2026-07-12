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
//  ------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Containers.Dapr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dapr.Testcontainers.Telemetry;

/// <summary>
/// In-process telemetry collector for Dapr integration tests.
/// Captures Zipkin v2 spans exported by daprd and optionally captures .NET
/// <see cref="ActivitySource"/> spans from the test application process.
/// </summary>
public sealed class DaprTelemetryCollector : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentQueue<ZipkinSpan> _zipkinSpans = new();
    private readonly ConcurrentQueue<CapturedActivity> _activities = new();
    private readonly int _port;
    private WebApplication? _app;
    private ActivityListener? _activityListener;

    /// <summary>
    /// Initializes a new telemetry collector on a random available host port.
    /// </summary>
    public DaprTelemetryCollector() : this(PortUtilities.GetAvailablePort())
    {
    }

    /// <summary>
    /// Initializes a new telemetry collector on the specified host port.
    /// </summary>
    public DaprTelemetryCollector(int port)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(port, 0, nameof(port));
        _port = port;
    }

    /// <summary>
    /// Host port used by the collector.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Zipkin endpoint address reachable from a daprd container.
    /// </summary>
    public string ZipkinEndpointAddressForDapr =>
        $"http://{DaprdContainer.ContainerHostAlias}:{_port}/api/v2/spans";

    /// <summary>
    /// Captured Zipkin spans exported by daprd.
    /// </summary>
    public IReadOnlyList<ZipkinSpan> ZipkinSpans => _zipkinSpans.ToArray();

    /// <summary>
    /// Captured .NET activities from configured activity sources.
    /// </summary>
    public IReadOnlyList<CapturedActivity> Activities => _activities.ToArray();

    /// <summary>
    /// Starts the Zipkin-compatible HTTP collector.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");

        var app = builder.Build();
        app.MapPost("/api/v2/spans", async (HttpRequest request) =>
        {
            var spans = await JsonSerializer.DeserializeAsync<List<ZipkinSpan>>(
                request.Body,
                JsonSerializerOptions,
                request.HttpContext.RequestAborted);

            if (spans is not null)
            {
                foreach (var span in spans)
                {
                    _zipkinSpans.Enqueue(span);
                }
            }

            return Results.Accepted();
        });

        await app.StartAsync(cancellationToken);
        _app = app;
    }

    /// <summary>
    /// Captures completed activities from the named source.
    /// </summary>
    public void CaptureActivitySource(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        if (_activityListener is null)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => string.Equals(source.Name, sourceName, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = CaptureActivity
            };
            ActivitySource.AddActivityListener(_activityListener);
            return;
        }

        var existingListener = _activityListener;
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source =>
                existingListener.ShouldListenTo?.Invoke(source) == true ||
                string.Equals(source.Name, sourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = CaptureActivity
        };
        ActivitySource.AddActivityListener(_activityListener);
        existingListener.Dispose();
    }

    /// <summary>
    /// Waits until a captured Zipkin span matches the predicate.
    /// </summary>
    public Task<ZipkinSpan> WaitForZipkinSpanAsync(
        Func<ZipkinSpan, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        WaitForAsync(
            () => _zipkinSpans.FirstOrDefault(predicate),
            timeout,
            cancellationToken,
            () => $"Captured Zipkin spans: {_zipkinSpans.Count}; spans: {string.Join(", ", _zipkinSpans.Select(span => $"{span.TraceId}/{span.Name}/{span.Kind}").Take(10))}");

    /// <summary>
    /// Waits until a captured .NET activity matches the predicate.
    /// </summary>
    public Task<CapturedActivity> WaitForActivityAsync(
        Func<CapturedActivity, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        WaitForAsync(
            () => _activities.FirstOrDefault(predicate),
            timeout,
            cancellationToken,
            () => $"Captured activities: {_activities.Count}; activities: {string.Join(", ", _activities.Select(activity => $"{activity.SourceName}/{activity.DisplayName}/{activity.TraceId}").Take(10))}");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _activityListener?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private void CaptureActivity(Activity activity)
    {
        _activities.Enqueue(new CapturedActivity(
            activity.Source.Name,
            activity.DisplayName,
            activity.TraceId.ToHexString(),
            activity.SpanId.ToHexString(),
            activity.ParentSpanId.ToHexString(),
            activity.ParentId,
            activity.Kind,
            activity.Status,
            activity.StartTimeUtc,
            activity.Duration,
            activity.Tags.ToDictionary(tag => tag.Key, tag => (object?)tag.Value)));
    }

    private static async Task<T> WaitForAsync<T>(
        Func<T?> probe,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<string>? diagnostics = null)
        where T : class
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        while (!linkedCts.IsCancellationRequested)
        {
            var result = probe();
            if (result is not null)
            {
                return result;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var diagnosticText = diagnostics?.Invoke();
        throw new TimeoutException(
            string.IsNullOrWhiteSpace(diagnosticText)
                ? $"Telemetry condition was not satisfied within {timeout}."
                : $"Telemetry condition was not satisfied within {timeout}. {diagnosticText}");
    }
}
