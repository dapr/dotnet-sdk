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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using Grpc.Core;
using Xunit;

namespace Dapr.Common.Test;

public sealed class DaprClientUtilitiesTests
{
    [Fact]
    public void ConfigureGrpcCallOptions_ShouldIncludeW3CTraceContext_WhenActivityCurrentIsSet()
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
        }
        finally
        {
            Activity.Current = previous;
        }
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
}
