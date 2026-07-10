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
/// Notifies one party that a document has been approved. Called once per party as the workflow's
/// fan-out step. Idempotent: re-running it just logs again.
/// </summary>
public sealed partial class NotifyPartiesActivity(ILogger<NotifyPartiesActivity> logger) : WorkflowActivity<PartyNotification, object?>
{
    public override Task<object?> RunAsync(WorkflowActivityContext context, PartyNotification input)
    {
        LogNotification(input.Party, input.DocumentId);
        return Task.FromResult<object?>(null);
    }

    [LoggerMessage(LogLevel.Information, "Notifying {Party} about approved document {DocumentId}")]
    private partial void LogNotification(string party, string documentId);
}
