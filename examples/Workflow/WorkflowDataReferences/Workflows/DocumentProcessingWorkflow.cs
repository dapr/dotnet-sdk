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
using WorkflowDataReferences.Activities;

namespace WorkflowDataReferences.Workflows;

/// <summary>
/// Shared by the inline and automatic examples: the same workflow processes ordinary documents in both.
/// </summary>
/// <remarks>
/// When the host selects <c>PreferReference</c> with <c>WithBinaryStorePayloads</c>, the SDK stores serialized values
/// exceeding the configured threshold in a binary store and records reference envelopes in history.
/// It resolves those references before delivering documents to this workflow or its activity.
/// Storage I/O stays outside deterministic workflow execution. Without opt-in, the same code
/// runs with inline data; the short custom status stays inline in either mode.
/// </remarks>
internal sealed class DocumentProcessingWorkflow : Workflow<DocumentPayload, DocumentPayload>
{
    public override async Task<DocumentPayload> RunAsync(WorkflowContext context, DocumentPayload input)
    {
        // Boundary 1: the SDK has already deserialized the workflow input, even if it was offloaded.
        context.SetCustomStatus("Processing document");
        // Boundaries 2 and 3: activity input/output use the same ordinary object contracts.
        // The configured sending policy decides whether their serialized bytes travel inline or by reference.
        var result = await context.CallActivityAsync<DocumentPayload>(nameof(UppercaseDocumentActivity), input);
        context.SetCustomStatus("Document processed");
        // Boundary 4: this is an in-memory object. Offloading the return value is not a streaming write.
        return result;
    }
}
