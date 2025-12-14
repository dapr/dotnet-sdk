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

public class StartWorkflowOptionsTests
{
    [Fact]
    public void NewInstance_ShouldHaveNullInstanceId_AndNullStartAt()
    {
        var options = new StartWorkflowOptions();

        Assert.Null(options.InstanceId);
        Assert.Null(options.StartAt);
    }

    [Fact]
    public void Properties_ShouldRoundTrip_WhenAssigned()
    {
        var expectedInstanceId = "my-instance-id";
        var expectedStartAt = new DateTimeOffset(2025, 01, 02, 03, 04, 05, TimeSpan.Zero);

        var options = new StartWorkflowOptions
        {
            InstanceId = expectedInstanceId,
            StartAt = expectedStartAt
        };

        Assert.Equal(expectedInstanceId, options.InstanceId);
        Assert.Equal(expectedStartAt, options.StartAt);
    }
}
