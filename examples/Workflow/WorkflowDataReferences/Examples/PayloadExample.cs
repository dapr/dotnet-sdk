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

/// <summary>Defines one independently configured, runnable payload-reference scenario.</summary>
internal abstract class PayloadExample
{
    /// <summary>Gets the command-line scenario name used by <c>--Example</c>.</summary>
    public abstract string Id { get; }
    /// <summary>Gets the heading printed before the scenario runs.</summary>
    public abstract string Title { get; }
    /// <summary>Gets the task this scenario is intended to teach.</summary>
    public abstract string WhenToUse { get; }
    /// <summary>Registers workflow services and the payload policy for this scenario's host.</summary>
    /// <param name="services">The host's service collection.</param>
    public abstract void ConfigureServices(IServiceCollection services);

    /// <summary>Runs the scenario after its host and workflow worker have started.</summary>
    /// <param name="services">The scenario's scoped service provider.</param>
    /// <param name="document">The shared document fixture; raw-byte scenarios may generate their own data.</param>
    /// <param name="cancellationToken">The combined application-shutdown and scenario-timeout token.</param>
    /// <returns>A task that completes after the scenario's result has been inspected.</returns>
    public abstract Task RunAsync(IServiceProvider services, DocumentPayload document,
        CancellationToken cancellationToken);

    /// <summary>Waits for a workflow and rejects terminal states other than successful completion.</summary>
    /// <param name="client">The configured workflow client.</param>
    /// <param name="instanceId">The instance to wait for.</param>
    /// <param name="getInputsAndOutputs">Whether to prepare materialized input/output values; false before streaming reads.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The successfully completed workflow state.</returns>
    /// <exception cref="InvalidOperationException">The workflow did not complete successfully.</exception>
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

    /// <summary>Checks and prints the uppercase result of an ordinary-object workflow.</summary>
    /// <param name="state">The completed state with materialized output.</param>
    /// <param name="original">The original input document.</param>
    protected static void PrintDocumentResult(WorkflowState state, DocumentPayload original)
    {
        var processed = state.ReadOutputAs<DocumentPayload>()
                        ?? throw new InvalidOperationException("The workflow returned no document.");
        if (processed != original with { Content = original.Content.ToUpperInvariant() })
            throw new InvalidOperationException("The workflow returned an unexpected document.");
        Console.WriteLine($"Result: {processed.Content.Length} characters, first character '{processed.Content[0]}'.");
    }

    /// <summary>Checks an inline byte-count/hash summary against its input reference.</summary>
    /// <param name="state">The completed state containing the summary.</param>
    /// <param name="inputReference">The reference whose bytes the activity consumed.</param>
    /// <returns>The verified summary.</returns>
    protected static PayloadSummary ReadReferenceResult(
        WorkflowState state, PayloadReference<DocumentPayload> inputReference)
    {
        var summary = state.ReadOutputAs<PayloadSummary>()
                      ?? throw new InvalidOperationException("The streaming workflow returned no summary.");
        if (summary.ByteCount != inputReference.Size || summary.Sha256 != inputReference.ContentHash)
            throw new InvalidOperationException("The streaming result does not match the stored document.");

        return summary;
    }

    /// <summary>Prints the verified streaming summary and explains the metadata-hydration tradeoff.</summary>
    /// <param name="state">The completed state containing the summary.</param>
    /// <param name="inputReference">The reference whose bytes the activity consumed.</param>
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
