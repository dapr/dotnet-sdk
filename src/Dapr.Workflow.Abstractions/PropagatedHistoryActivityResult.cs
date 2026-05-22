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
/// <param name="Started">Whether the activity was scheduled in the propagated history.</param>
/// <param name="Completed">Whether the activity completed successfully.</param>
/// <param name="Failed">Whether the activity failed.</param>
/// <param name="Input">The JSON-encoded input payload, or <c>null</c> when unset.</param>
/// <param name="Output">The JSON-encoded output payload, or <c>null</c> when the activity has not completed.</param>
/// <param name="FailureDetails">The failure details when <paramref name="Failed"/> is true, otherwise <c>null</c>.</param>
/// <remarks>
/// Mirrors the <c>ActivityResult</c> type in the Go and Python SDKs so cross-language
/// quickstarts and audit patterns line up.
/// </remarks>
public sealed record PropagatedHistoryActivityResult(
    string Name,
    bool Started,
    bool Completed,
    bool Failed,
    string? Input,
    string? Output,
    WorkflowTaskFailureDetails? FailureDetails);
