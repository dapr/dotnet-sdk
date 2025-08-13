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

using Dapr.DurableTask;
using Moq;

namespace Dapr.Workflow.Test;

public class DaprWorkflowClientTests
{
    [Fact]
    public async Task ScheduleNewWorkflowAsync_DateTimeKindUnspecified_AssumesLocalTime()
    {
        var innerClient = new Mock<GrpcDurableTaskClientWrapper>();

        var name = "test-workflow";
        var instanceId = "test-instance-id";
        var input = "test-input";
        var startTime = new DateTime(2025, 07, 10);

        Assert.Equal(DateTimeKind.Unspecified, startTime.Kind);

        innerClient.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
            It.IsAny<TaskName>(),
            It.IsAny<object?>(),
            It.IsAny<StartOrchestrationOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback((TaskName n, object? i, StartOrchestrationOptions? o, CancellationToken ct) =>
            {
                Assert.Equal(name, n);
                Assert.Equal(input, i);
                Assert.NotNull(o);
                Assert.NotNull(o.StartAt);
                // options configured with local time
                Assert.Equal(new DateTimeOffset(startTime, TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)), o.StartAt.Value);
            })
            .ReturnsAsync("instance-id");

        var client = new DaprWorkflowClient(innerClient.Object);

        await client.ScheduleNewWorkflowAsync(name, instanceId, input, startTime);
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_DateTimeKindUtc_PreservedAsUtc()
    {
        var innerClient = new Mock<GrpcDurableTaskClientWrapper>();

        var name = "test-workflow";
        var instanceId = "test-instance-id";
        var input = "test-input";
        var startTime = new DateTime(2025, 07, 10, 1, 30, 30, DateTimeKind.Utc);

        Assert.Equal(DateTimeKind.Utc, startTime.Kind);

        innerClient.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
            It.IsAny<TaskName>(),
            It.IsAny<object?>(),
            It.IsAny<StartOrchestrationOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback((TaskName n, object? i, StartOrchestrationOptions? o, CancellationToken ct) =>
            {
                Assert.Equal(name, n);
                Assert.Equal(input, i);
                Assert.NotNull(o);
                Assert.NotNull(o.StartAt);
                // options configured with UTC time
                Assert.Equal(new DateTimeOffset(startTime, TimeSpan.Zero), o.StartAt.Value);
            })
            .ReturnsAsync("instance-id");

        var client = new DaprWorkflowClient(innerClient.Object);

        await client.ScheduleNewWorkflowAsync(name, instanceId, input, startTime);
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_DateTimeOffset_SetsStartAt()
    {
        var innerClient = new Mock<GrpcDurableTaskClientWrapper>();

        var name = "test-workflow";
        var instanceId = "test-instance-id";
        var input = "test-input";
        var startTime = new DateTimeOffset(2025, 07, 10, 1, 30, 30, TimeSpan.FromHours(3));

        innerClient.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
            It.IsAny<TaskName>(),
            It.IsAny<object?>(),
            It.IsAny<StartOrchestrationOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback((TaskName n, object? i, StartOrchestrationOptions? o, CancellationToken ct) =>
            {
                Assert.Equal(name, n);
                Assert.Equal(input, i);
                Assert.NotNull(o);
                Assert.NotNull(o.StartAt);
                // options configured with specified offset
                Assert.Equal(startTime, o.StartAt.Value);
            })
            .ReturnsAsync("instance-id");

        var client = new DaprWorkflowClient(innerClient.Object);

        await client.ScheduleNewWorkflowAsync(name, instanceId, input, startTime);
    }
}
