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

namespace Dapr.Workflow;

/// <summary>
/// A reconstructed view of a single activity invocation surfaced through propagated workflow history.
/// </summary>
/// <param name="Name">The scheduled name of the activity.</param>
/// <param name="Status">The resolved lifecycle status of this activity.</param>
/// <param name="Input">The JSON-encoded input payload, or <c>null</c> when unset.</param>
/// <param name="Output">The JSON-encoded output payload, or <c>null</c> when the activity has not completed.</param>
/// <param name="FailureDetails">The failure details when <paramref name="Status"/> is <see cref="PropagatedHistoryTaskStatus.Failed"/>, otherwise <c>null</c>.</param>
/// <remarks>
/// Every activity surfaced through propagated history was scheduled, so <see cref="Status"/>
/// reflects how far it progressed past scheduling: <see cref="PropagatedHistoryTaskStatus.Pending"/>
/// means it was scheduled but has not yet completed or failed, <see cref="PropagatedHistoryTaskStatus.Completed"/>
/// means it succeeded, and <see cref="PropagatedHistoryTaskStatus.Failed"/> means it failed.
/// </remarks>
public sealed record PropagatedHistoryActivityResult(
    string Name,
    PropagatedHistoryTaskStatus Status,
    string? Input,
    string? Output,
    WorkflowTaskFailureDetails? FailureDetails);
