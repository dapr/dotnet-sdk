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

namespace Dapr.Actors.Next.Abstractions.Dispatching;

/// <summary>
/// Describes a dispatch request delivered to generated dispatcher code.
/// </summary>
/// <remarks>
/// <see cref="Payload"/> carries the raw UTF-8 JSON argument bytes. Dispatchers deserialize directly from
/// these bytes so the runtime does not transcode through an intermediate JSON string on the hot path.
/// </remarks>
public readonly record struct ActorDispatchRequest(
    string ActorType,
    ActorId ActorId,
    string MethodName,
    ReadOnlyMemory<byte> Payload,
    IReadOnlyDictionary<string, string> Headers,
    ActorRequestContext RequestContext);
