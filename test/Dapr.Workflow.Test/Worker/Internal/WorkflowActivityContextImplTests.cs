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

using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Worker.Internal;

namespace Dapr.Workflow.Test.Worker.Internal;

public class WorkflowActivityContextImplTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInstanceIdIsNull()
    {
        var identifier = new TaskIdentifier("ActivityA");

        Assert.Throws<ArgumentNullException>(() => new WorkflowActivityContextImpl(identifier, null!, "tek"));
    }

    [Fact]
    public void Properties_ShouldExposeIdentifierAndInstanceId()
    {
        var identifier = new TaskIdentifier("ActivityA");
        var context = new WorkflowActivityContextImpl(identifier, "instance-1", "tek");

        Assert.Equal(identifier, context.Identifier);
        Assert.Equal("instance-1", context.InstanceId);
        Assert.Equal("tek", context.TaskExecutionKey);
    }
}
