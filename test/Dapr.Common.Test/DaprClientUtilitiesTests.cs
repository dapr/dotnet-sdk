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
// ------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using Dapr.Common;
using Grpc.Core;
using Shouldly;
using Xunit;

namespace Dapr.Common.Test;

public sealed class DaprClientUtilitiesTests
{
    [Fact]
    public void ConfigureGrpcCallOptions_ShouldIncludeTraceContextHeaders_WhenActivityCurrentExists()
    {
        // Arrange
        const int traceIdLength = 16;
        const int spanIdLength = 8;
        const int grpcTraceBinHeaderLength = 29;
        Activity.Current = null;
        using var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        // Act
        var callOptions = DaprClientUtilities.ConfigureGrpcCallOptions(
            typeof(DaprClientUtilitiesTests).Assembly,
            daprApiToken: null,
            TestContext.Current.CancellationToken);

        // Assert
        var headers = callOptions.Headers;
        headers.ShouldNotBeNull();

        var grpcTraceBin = headers.First(header => header.Key == "grpc-trace-bin").ValueBytes;
        var expectedTraceId = new byte[traceIdLength];
        activity.TraceId.CopyTo(expectedTraceId);
        var expectedSpanId = new byte[spanIdLength];
        activity.SpanId.CopyTo(expectedSpanId);

        grpcTraceBin.Length.ShouldBe(grpcTraceBinHeaderLength);
        grpcTraceBin[0].ShouldBe((byte)0);
        grpcTraceBin[1].ShouldBe((byte)0);
        grpcTraceBin.AsSpan(2, traceIdLength).SequenceEqual(expectedTraceId).ShouldBeTrue();
        grpcTraceBin[18].ShouldBe((byte)1);
        grpcTraceBin.AsSpan(19, spanIdLength).SequenceEqual(expectedSpanId).ShouldBeTrue();
        grpcTraceBin[27].ShouldBe((byte)2);
        grpcTraceBin[28].ShouldBe((byte)activity.ActivityTraceFlags);
    }

    [Fact]
    public void ConfigureGrpcCallOptions_ShouldNotIncludeTraceContextHeaders_WhenActivityCurrentIsMissing()
    {
        // Arrange
        Activity.Current = null;

        // Act
        var callOptions = DaprClientUtilities.ConfigureGrpcCallOptions(
            typeof(DaprClientUtilitiesTests).Assembly,
            daprApiToken: null,
            TestContext.Current.CancellationToken);

        // Assert
        var headers = callOptions.Headers;
        headers.ShouldNotBeNull();
        headers.Any(header => header.Key == "grpc-trace-bin")
            .ShouldBeFalse();
    }

    [Fact]
    public void AddCurrentTraceContextHeaders_ShouldNotDuplicateExistingHeaders()
    {
        // Arrange
        Activity.Current = null;
        using var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var existingGrpcTraceBin = new byte[] { 1, 2, 3 };
        var headers = new Metadata
        {
            { "grpc-trace-bin", existingGrpcTraceBin },
        };

        // Act
        DaprClientUtilities.AddCurrentTraceContextHeaders(headers);

        // Assert
        headers.Count(header => header.Key == "grpc-trace-bin").ShouldBe(1);
        headers.First(header => header.Key == "grpc-trace-bin").ValueBytes.ShouldBe(existingGrpcTraceBin);
    }
}
