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

namespace WorkflowDataReferences;

/// <summary>
/// Application data that can travel inline or through an SDK-managed data reference
/// without changing the workflow's input and output types.
/// </summary>
/// <param name="Name">The document name, serialized together with its content.</param>
/// <param name="Content">Text large enough in this example to exceed the automatic offloading threshold.</param>
internal sealed record DocumentPayload(string Name, string Content);

/// <summary>
/// A small, inline result describing stored serialized bytes rather than returning the full document.
/// </summary>
/// <param name="ByteCount">The number of bytes read from the data reference, including JSON structure.</param>
/// <param name="Sha256">The SHA-256 of those bytes, verified against the reference's recorded hash.</param>
internal sealed record PayloadSummary(long ByteCount, string Sha256);
