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

using System.Runtime.ExceptionServices;
using Dapr.Common.Serialization;

namespace Dapr.Workflow.Payloads;

internal sealed record PreparedWorkflowPayload(
    string? Wire,
    string? Serialized,
    ExceptionDispatchInfo? ReadError = null)
{
    internal T? ReadAs<T>(IDaprSerializer serializer)
    {
        if (PayloadReference.IsReferenceType(typeof(T)))
        {
            return (T?)WorkflowPayloadHandler.ReadReference(Wire, typeof(T), serializer);
        }

        ReadError?.Throw();
        return string.IsNullOrEmpty(Serialized) ? default : serializer.Deserialize<T>(Serialized);
    }
}
