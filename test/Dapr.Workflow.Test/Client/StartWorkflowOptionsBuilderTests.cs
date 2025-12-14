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

using Dapr.Workflow.Client;

namespace Dapr.Workflow.Test.Client;

public class StartWorkflowOptionsBuilderTests
{
    [Fact]
    public void Build_ShouldReturnOptionsWithNullValues_WhenNothingConfigured()
    {
        var builder = new StartWorkflowOptionsBuilder();

        var options = builder.Build();

        Assert.NotNull(options);
        Assert.Null(options.InstanceId);
        Assert.Null(options.StartAt);
    }

    [Fact]
    public void WithInstanceId_ShouldSetInstanceId_OnBuiltOptions()
    {
        var builder = new StartWorkflowOptionsBuilder()
            .WithInstanceId("instance-123");

        var options = builder.Build();

        Assert.Equal("instance-123", options.InstanceId);
    }

    [Fact]
    public void StartAt_ShouldSetStartAt_OnBuiltOptions()
    {
        var startAt = new DateTimeOffset(2026, 02, 03, 04, 05, 06, TimeSpan.Zero);

        var builder = new StartWorkflowOptionsBuilder()
            .StartAt(startAt);

        var options = builder.Build();

        Assert.Equal(startAt, options.StartAt);
    }

    [Fact]
    public void StartAfter_ShouldSetStartAtCloseToUtcNowPlusDelay_OnBuiltOptions()
    {
        var delay = TimeSpan.FromSeconds(2);

        var before = DateTimeOffset.UtcNow;
        var builder = new StartWorkflowOptionsBuilder().StartAfter(delay);
        var after = DateTimeOffset.UtcNow;

        var options = builder.Build();

        Assert.NotNull(options.StartAt);

        var expectedLowerBound = before.Add(delay);
        var expectedUpperBound = after.Add(delay);

        Assert.InRange(options.StartAt!.Value, expectedLowerBound, expectedUpperBound);
    }

    [Fact]
    public void ImplicitConversion_ShouldBuildOptions()
    {
        var startAt = new DateTimeOffset(2027, 03, 04, 05, 06, 07, TimeSpan.Zero);

        StartWorkflowOptions options = new StartWorkflowOptionsBuilder()
            .WithInstanceId("instance-abc")
            .StartAt(startAt);

        Assert.Equal("instance-abc", options.InstanceId);
        Assert.Equal(startAt, options.StartAt);
    }

    [Fact]
    public void FluentCalls_ShouldReturnSameBuilderInstance_ForChaining()
    {
        var builder = new StartWorkflowOptionsBuilder();

        var returned1 = builder.WithInstanceId("x");
        var returned2 = builder.StartAt(DateTimeOffset.UtcNow);

        Assert.Same(builder, returned1);
        Assert.Same(builder, returned2);
    }
}
