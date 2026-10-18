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

using System.Collections.Generic;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Describes one transformation that was applied to a payload before it was stored.
/// </summary>
/// <param name="Name">The stable name of the stage, for example <c>gzip</c> or <c>aes-gcm</c>.</param>
/// <param name="Metadata">
/// Stage-specific values required to reverse the transformation, such as an initialization vector
/// or key version. <see langword="null"/> if the stage requires none.
/// </param>
public sealed record PayloadStage(string Name, IReadOnlyDictionary<string, string>? Metadata = null);
