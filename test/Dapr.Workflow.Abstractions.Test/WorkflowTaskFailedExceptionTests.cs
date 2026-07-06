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
// ------------------------------------------------------------------------

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowTaskFailedExceptionTests
{
    [Fact]
    public void Constructor_Sets_Message_And_FailureDetails()
    {
        var details = new WorkflowTaskFailureDetails(typeof(InvalidOperationException).FullName!, "boom");
        var ex = new WorkflowTaskFailedException("task failed", details);

        Assert.Equal("task failed", ex.Message);
        Assert.Same(details, ex.FailureDetails);
    }

    [Fact]
    public void Constructor_With_Null_FailureDetails_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new WorkflowTaskFailedException("task failed", null!));
    }
}
