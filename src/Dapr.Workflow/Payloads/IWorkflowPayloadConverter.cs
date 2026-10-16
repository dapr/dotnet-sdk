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

using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Transforms serialized payloads, independently of application types and serialization settings.
/// The worker awaits this codec outside deterministic workflow execution. Implementations must
/// preserve empty payloads and existing references on encode, and support historical formats on decode.
/// </summary>
internal interface IWorkflowPayloadConverter
{
    ValueTask<string> EncodeAsync(string serialized, PayloadSite site, CancellationToken cancellationToken = default);

    ValueTask<string?> DecodeAsync(string? wire, PayloadSite site, CancellationToken cancellationToken = default);
}
