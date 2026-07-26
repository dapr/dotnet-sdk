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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Grpc.Core;
using Xunit;

namespace Dapr.Common.Test;

public sealed class DaprClientUtilitiesTests
{
    private const int TraceIdLength = 16;
    private const int SpanIdLength = 8;
    private const int GrpcTraceBinHeaderLength = 29;

    [Fact]
    public void ConfigureGrpcCallOptions_ShouldIncludeTraceContext_WhenActivityCurrentIsSet()
    {
        var previous = Activity.Current;
        using var activity = new Activity("parent");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.TraceStateString = "vendor=value";
        activity.Start();

        try
        {
            var options = DaprClientUtilities.ConfigureGrpcCallOptions(
                typeof(DaprClientUtilitiesTests).Assembly,
                daprApiToken: null,
                CancellationToken.None);

            Assert.True(TryGetHeader(options.Headers, "traceparent", out var traceParent));
            Assert.Equal(activity.Id, traceParent);
            Assert.True(TryGetHeader(options.Headers, "tracestate", out var traceState));
            Assert.Equal(activity.TraceStateString, traceState);
            Assert.True(TryGetBinaryHeader(options.Headers, "grpc-trace-bin", out var grpcTraceBin));
            AssertGrpcTraceBinHeader(activity, grpcTraceBin);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Fact]
    public void ConfigureGrpcCallOptions_ShouldNotIncludeTraceContext_WhenActivityCurrentIsNull()
    {
        var previous = Activity.Current;
        Activity.Current = null;

        try
        {
            var options = DaprClientUtilities.ConfigureGrpcCallOptions(
                typeof(DaprClientUtilitiesTests).Assembly,
                daprApiToken: null,
                CancellationToken.None);

            Assert.False(TryGetHeader(options.Headers, "traceparent", out _));
            Assert.False(TryGetHeader(options.Headers, "tracestate", out _));
            Assert.False(TryGetBinaryHeader(options.Headers, "grpc-trace-bin", out _));
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Fact]
    public void AddTraceContextHeaders_ShouldNotDuplicateGrpcTraceBinHeader()
    {
        var previous = Activity.Current;
        using var activity = new Activity("parent");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var existingGrpcTraceBin = new byte[] { 1, 2, 3 };
        var headers = new Metadata
        {
            { "grpc-trace-bin", existingGrpcTraceBin },
        };

        try
        {
            DaprClientUtilities.AddTraceContextHeaders(headers);

            Assert.Equal(1, headers.Count(header => header.Key == "grpc-trace-bin"));
            Assert.True(TryGetBinaryHeader(headers, "grpc-trace-bin", out var grpcTraceBin));
            Assert.Equal(existingGrpcTraceBin, grpcTraceBin);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    private static void AssertGrpcTraceBinHeader(Activity activity, byte[] grpcTraceBin)
    {
        var expectedTraceId = new byte[TraceIdLength];
        activity.TraceId.CopyTo(expectedTraceId);
        var expectedSpanId = new byte[SpanIdLength];
        activity.SpanId.CopyTo(expectedSpanId);

        Assert.Equal(GrpcTraceBinHeaderLength, grpcTraceBin.Length);
        Assert.Equal(0, grpcTraceBin[0]);
        Assert.Equal(0, grpcTraceBin[1]);
        Assert.True(grpcTraceBin.AsSpan(2, TraceIdLength).SequenceEqual(expectedTraceId));
        Assert.Equal(1, grpcTraceBin[18]);
        Assert.True(grpcTraceBin.AsSpan(19, SpanIdLength).SequenceEqual(expectedSpanId));
        Assert.Equal(2, grpcTraceBin[27]);
        Assert.Equal((byte)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded), grpcTraceBin[28]);
    }

    private static bool TryGetHeader(Metadata metadata, string key, out string value)
    {
        value = string.Empty;
        var entry = metadata.FirstOrDefault(item => item.Key == key);
        if (entry is null)
        {
            return false;
        }

        value = entry.Value;
        return true;
    }

    private static bool TryGetBinaryHeader(Metadata metadata, string key, out byte[] value)
    {
        value = [];
        var entry = metadata.FirstOrDefault(item => item.Key == key);
        if (entry is null)
        {
            return false;
        }

        value = entry.ValueBytes;
        return true;
    }
}
