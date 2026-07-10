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

using Dapr.Workflow;

namespace Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;

/// <summary>
/// The compensation step: undoes any partial provisioning made before the charge failed. Runs only on
/// the failure path and must be idempotent.
/// </summary>
public sealed partial class ReleaseReservationActivity(ILogger<ReleaseReservationActivity> logger) : WorkflowActivity<ReleaseRequest, object?>
{
    public override Task<object?> RunAsync(WorkflowActivityContext context, ReleaseRequest input)
    {
        LogReleasingInformation(input.Amount, input.DocumentId);
        return Task.FromResult<object?>(null);
    }

    [LoggerMessage(LogLevel.Information, "Released reservation of {Amount:C} for document {DocumentId}")]
    private partial void LogReleasingInformation(decimal amount, string documentId);
}
