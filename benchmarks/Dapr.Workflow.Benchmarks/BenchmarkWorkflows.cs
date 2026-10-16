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

using System.Security.Cryptography;
using System.Text;
using Dapr.Common.Serialization;
using Dapr.Workflow;
using Dapr.Workflow.Payloads;

namespace Dapr.Workflow.Benchmarks;

internal enum BenchmarkMode { Inline, Automatic, References }

internal sealed record BenchmarkDocument(string Content)
{
    internal const int SerializedSize = 2_000_000;

    internal static BenchmarkDocument Create()
    {
        var serializer = new JsonDaprSerializer();
        var overhead = Encoding.UTF8.GetByteCount(serializer.Serialize(new BenchmarkDocument("")));
        var random = new Random(42);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var content = new char[SerializedSize - overhead];
        for (var i = 0; i < content.Length; i++)
            content[i] = alphabet[random.Next(alphabet.Length)];

        var document = new BenchmarkDocument(new string(content));
        if (Encoding.UTF8.GetByteCount(serializer.Serialize(document)) != SerializedSize)
            throw new InvalidOperationException("The serialized benchmark document must be exactly 2 MB.");
        return document;
    }

    internal string Checksum() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Content)));
}

/// <summary>
/// Receives a materialized document on every orchestration execution. The same workflow
/// runs with inline transport or automatic offloading, depending on host registration.
/// </summary>
internal sealed class MaterializedWorkflow : Workflow<BenchmarkDocument, string>
{
    public override async Task<string> RunAsync(WorkflowContext context, BenchmarkDocument input)
    {
        for (var step = 1; step <= 8; step++)
            await context.CallActivityAsync<int>(nameof(CoordinateActivity), step);
        return await context.CallActivityAsync<string>(nameof(ProcessDocumentActivity), input);
    }
}

/// <summary>
/// Keeps only a reference through eight coordination activities; activity nine is the
/// first consumer that needs the document. Replay uses the recorded activity results.
/// </summary>
internal sealed class ReferenceWorkflow : Workflow<PayloadReference<BenchmarkDocument>, string>
{
    public override async Task<string> RunAsync(WorkflowContext context, PayloadReference<BenchmarkDocument> input)
    {
        for (var step = 1; step <= 8; step++)
            await context.CallActivityAsync<int>(nameof(CoordinateActivity), step);
        return await context.CallActivityAsync<string>(nameof(ProcessReferenceActivity), input);
    }
}

internal sealed class CoordinateActivity : WorkflowActivity<int, int>
{
    public override Task<int> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input);
}

internal sealed class ProcessDocumentActivity : WorkflowActivity<BenchmarkDocument, string>
{
    public override Task<string> RunAsync(WorkflowActivityContext context, BenchmarkDocument input)
        => Task.FromResult(input.Checksum());
}

internal sealed class ProcessReferenceActivity(IWorkflowPayloadResolver resolver)
    : WorkflowActivity<PayloadReference<BenchmarkDocument>, string>
{
    public override async Task<string> RunAsync(WorkflowActivityContext context, PayloadReference<BenchmarkDocument> input)
        => (await resolver.ResolveAsync<BenchmarkDocument>(input)).Checksum();
}

internal sealed class ConnectionWarmupWorkflow : Workflow<int, int>
{
    public override Task<int> RunAsync(WorkflowContext context, int input) => Task.FromResult(input);
}
