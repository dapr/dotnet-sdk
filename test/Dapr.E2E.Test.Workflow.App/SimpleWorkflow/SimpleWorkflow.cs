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

using Dapr.E2E.Test.Workflow.App.SimpleWorkflow.Activities;
using Dapr.Workflow;

namespace Dapr.E2E.Test.Workflow.App.SimpleWorkflow;

public sealed class SimpleWorkflow : Workflow<SimpleWorkflowInput, int>
{
    public override async Task<int> RunAsync(WorkflowContext context, SimpleWorkflowInput input)
    {
        var activityResult = await context.CallActivityAsync<int>(nameof(AddNumbersActivity),
            new AddNumbersInput(input.Operand1, input.Operand2));

        return activityResult;
    }
}

public sealed record SimpleWorkflowInput(int Operand1, int Operand2);
