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
using Dapr.Workflow.Payloads;
using Microsoft.Extensions.DependencyInjection;

namespace WorkflowDataReferences.Examples;

internal abstract class PayloadExample
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string WhenToUse { get; }
    public abstract void ConfigureServices(IServiceCollection services);

    public abstract Task RunAsync(IServiceProvider services, DocumentPayload document,
        CancellationToken cancellationToken);

    protected static async Task<WorkflowState> WaitForCompletionAsync(
        DaprWorkflowClient client, string instanceId, bool getInputsAndOutputs, CancellationToken cancellationToken)
    {
        // Make the read choice explicit at each call site. true prepares/materializes referenced
        // inputs and outputs; false is appropriate before the separate client chunk API.
        // Existing wait APIs can still hydrate custom status; these examples keep it small and inline.
        var state = await client.WaitForWorkflowCompletionAsync(instanceId, getInputsAndOutputs, cancellationToken);
        if (state.RuntimeStatus != WorkflowRuntimeStatus.Completed)
            throw new InvalidOperationException(
                $"Workflow '{instanceId}' finished with status {state.RuntimeStatus}: {state.FailureDetails?.ErrorMessage}");
        return state;
    }

    protected static void PrintDocumentResult(WorkflowState state, DocumentPayload original)
    {
        var processed = state.ReadOutputAs<DocumentPayload>()
                        ?? throw new InvalidOperationException("The workflow returned no document.");
        if (processed != original with { Content = original.Content.ToUpperInvariant() })
            throw new InvalidOperationException("The workflow returned an unexpected document.");
        Console.WriteLine($"Result: {processed.Content.Length} characters, first character '{processed.Content[0]}'.");
    }

    protected static PayloadSummary ReadReferenceResult(
        WorkflowState state, PayloadReference<DocumentPayload> inputReference)
    {
        var summary = state.ReadOutputAs<PayloadSummary>()
                      ?? throw new InvalidOperationException("The streaming workflow returned no summary.");
        if (summary.ByteCount != inputReference.Size || summary.Sha256 != inputReference.ContentHash)
            throw new InvalidOperationException("The streaming result does not match the stored document.");

        return summary;
    }

    protected static void PrintReferenceResult(WorkflowState state, PayloadReference<DocumentPayload> inputReference)
    {
        var summary = ReadReferenceResult(state, inputReference);
        Console.WriteLine($"Result: activity streamed {summary.ByteCount} serialized bytes using async chunks.");
        Console.WriteLine($"SHA-256 verified: {summary.Sha256}");
        Console.WriteLine("Serialized bytes include JSON structure, not just the document's characters.");
        Console.WriteLine(
            "Client inspection: this typed summary read also hydrates the referenced input; it is not a streaming-only client.");
        Console.WriteLine(
            "Takeaway: the workflow never reads the document; the activity streams bytes instead of deserializing it.");
    }
}
