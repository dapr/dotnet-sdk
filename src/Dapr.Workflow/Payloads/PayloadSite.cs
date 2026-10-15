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

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Describes the context in which a payload is crossing a workflow boundary. Supplied to
/// <see cref="IWorkflowPayloadConverter"/> so an implementation can apply per-boundary policy
/// and derive storage keys scoped to the owning workflow instance.
/// </summary>
/// <param name="Kind">The boundary being crossed.</param>
/// <param name="InstanceId">The identifier of the workflow instance that owns the payload.</param>
/// <param name="Name">
/// The name of the workflow, activity, or event associated with the payload, where applicable.
/// <see langword="null"/> for boundaries that are not named, such as <see cref="PayloadKind.CustomStatus"/>.
/// </param>
public readonly record struct PayloadSite(PayloadKind Kind, string InstanceId, string? Name);
