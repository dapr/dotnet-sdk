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

using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Payloads;
using Dapr.Workflow.Worker.Internal;
using Moq;

namespace Dapr.Workflow.Test.Payloads;

public class WorkflowActivityContextPayloadTests
{
    [Fact]
    public async Task ContextForwardsAllOperationsAndSuppliesInstanceId()
    {
        var token = TestContext.Current.CancellationToken;
        var reference = new PayloadReference("store", "key", 3, "hash");
        var typed = reference.AsTyped<string>();
        using var stream = new MemoryStream();
        var chunks = new Mock<IAsyncEnumerable<ReadOnlyMemory<byte>>>().Object;
        Func<Stream, CancellationToken, Task> write = (_, _) => Task.CompletedTask;
        var resolver = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        resolver.Setup(r => r.ResolveAsync<string>(typed, token)).ReturnsAsync("typed");
        resolver.Setup(r => r.ResolveAsync<string>(reference, token)).ReturnsAsync("untyped");
        resolver.Setup(r => r.OpenAsync(reference, token)).ReturnsAsync(stream);
        resolver.Setup(r => r.ReadChunksAsync(reference, token)).Returns(chunks);
        resolver.Setup(r => r.ReadChunksAsync(reference, 42, token)).Returns(chunks);
        resolver.Setup(r => r.StoreAsync("value", "instance", token)).ReturnsAsync(typed);
        resolver.Setup(r => r.StoreStreamAsync(stream, "instance", token)).ReturnsAsync(reference);
        resolver.Setup(r => r.WriteAsync(write, "instance", token)).ReturnsAsync(reference);
        resolver.Setup(r => r.UploadAsync(write, "instance", token)).ReturnsAsync(reference);
        resolver.Setup(r => r.UploadAsync(stream, "instance", token)).ReturnsAsync(reference);
        resolver.Setup(r => r.UploadAsync(chunks, "instance", token)).ReturnsAsync(reference);
        var context = new WorkflowActivityContextImpl(new TaskIdentifier("activity"), "instance", "task", 1, resolver.Object);

        Assert.True(context.HasPayloadResolver());
        Assert.Equal("typed", await context.ResolvePayloadAsync(typed, token));
        Assert.Equal("untyped", await context.ResolvePayloadAsync<string>(reference, token));
        Assert.Same(stream, await context.OpenPayloadAsync(reference, token));
        Assert.Same(chunks, context.ReadPayloadChunksAsync(reference, token));
        Assert.Same(chunks, context.ReadPayloadChunksAsync(reference, 42, token));
        Assert.Same(typed, await context.StorePayloadAsync("value", token));
        Assert.Same(reference, await context.StorePayloadStreamAsync(stream, token));
        Assert.Same(reference, await context.WritePayloadAsync(write, token));
        Assert.Same(reference, await context.UploadPayloadAsync(write, token));
        Assert.Same(reference, await context.UploadPayloadAsync(stream, token));
        Assert.Same(reference, await context.UploadPayloadAsync(chunks, token));
        Assert.True(stream.CanRead);
        resolver.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextUsesWorkerTokenUnlessCallerSuppliesCancelableToken(bool explicitToken)
    {
        using var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var expected = explicitToken ? callerCancellation.Token : workerCancellation.Token;
        var supplied = explicitToken ? callerCancellation.Token : CancellationToken.None;
        var reference = new PayloadReference("store", "key", 0, "hash");
        var typed = reference.AsTyped<string>();
        using var stream = new MemoryStream();
        var chunks = new Mock<IAsyncEnumerable<ReadOnlyMemory<byte>>>().Object;
        Func<Stream, CancellationToken, Task> write = (_, _) => Task.CompletedTask;
        var resolver = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        resolver.Setup(r => r.ResolveAsync(typed, expected)).ReturnsAsync("value");
        resolver.Setup(r => r.ResolveAsync<string>(reference, expected)).ReturnsAsync("value");
        resolver.Setup(r => r.OpenAsync(reference, expected)).ReturnsAsync(stream);
        resolver.Setup(r => r.ReadChunksAsync(reference, expected)).Returns(chunks);
        resolver.Setup(r => r.ReadChunksAsync(reference, 42, expected)).Returns(chunks);
        resolver.Setup(r => r.StoreAsync("value", "instance", expected)).ReturnsAsync(typed);
        resolver.Setup(r => r.StoreStreamAsync(stream, "instance", expected)).ReturnsAsync(reference);
        resolver.Setup(r => r.WriteAsync(write, "instance", expected)).ReturnsAsync(reference);
        resolver.Setup(r => r.UploadAsync(write, "instance", expected)).ReturnsAsync(reference);
        resolver.Setup(r => r.UploadAsync(stream, "instance", expected)).ReturnsAsync(reference);
        resolver.Setup(r => r.UploadAsync(chunks, "instance", expected)).ReturnsAsync(reference);
        var context = new WorkflowActivityContextImpl(new TaskIdentifier("activity"), "instance", "task", 1,
            resolver.Object, workerCancellation.Token);

        Assert.Equal(workerCancellation.Token, context.CancellationToken);
        await context.ResolvePayloadAsync(typed, supplied);
        await context.ResolvePayloadAsync<string>(reference, supplied);
        await context.OpenPayloadAsync(reference, supplied);
        Assert.Same(chunks, context.ReadPayloadChunksAsync(reference, supplied));
        Assert.Same(chunks, context.ReadPayloadChunksAsync(reference, 42, supplied));
        await context.StorePayloadAsync("value", supplied);
        await context.StorePayloadStreamAsync(stream, supplied);
        await context.WritePayloadAsync(write, supplied);
        await context.UploadPayloadAsync(write, supplied);
        await context.UploadPayloadAsync(stream, supplied);
        await context.UploadPayloadAsync(chunks, supplied);
        resolver.VerifyAll();
    }

    [Fact]
    public async Task MissingSupportIsReportedWithoutChangingCustomContexts()
    {
        var context = new Mock<WorkflowActivityContext>().Object;
        Assert.False(context.HasPayloadResolver());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.WritePayloadAsync(
            (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken));
        Assert.Contains("WithBinaryStorePayloads", error.Message);
        Assert.Throws<ArgumentNullException>(() => WorkflowActivityContextPayloadExtensions.HasPayloadResolver(null!));
    }
}
