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
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow.Client;
using Dapr.Workflow.Payloads;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker.Internal;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dapr.Workflow.Test.Payloads;

public class PayloadWritePreferenceTests
{
    private const string Serialized = "{\"content\":\"document\"}";
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(Serialized);
    private static readonly PayloadReference<Document> Reference = new("store", "key", Bytes.Length,
        Convert.ToHexString(SHA256.HashData(Bytes)).ToLowerInvariant());
    private static readonly PayloadSite Site = new(PayloadKind.ActivityInput, "instance", "activity");

    public static TheoryData<string, PayloadWritePreference, bool, bool, string?> Calls
    {
        get
        {
            var data = new TheoryData<string, PayloadWritePreference, bool, bool, string?>();
            foreach (var kind in new[] { "activity", "child", "detached" })
            foreach (var preference in Enum.GetValues<PayloadWritePreference>())
            foreach (var reference in new[] { false, true })
            foreach (var allow in new[] { false, true })
            foreach (var target in new[] { null, "source", "destination" })
                data.Add(kind, preference, reference, allow, target);
            return data;
        }
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [MemberData(nameof(Calls))]
    public async Task CallsApplyPreferenceAndCrossAppOverride(
        string kind, PayloadWritePreference preference, bool referenceInput, bool allowReferences, string? target)
    {
        var token = TestContext.Current.CancellationToken;
        var requireInline = target == "destination" && !allowReferences;
        var resolve = referenceInput && (requireInline || preference == PayloadWritePreference.PreferInline);
        var offload = !referenceInput && !requireInline && preference == PayloadWritePreference.PreferReference;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        if (resolve)
            SetupRead(store);
        if (offload)
        {
            store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                    DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        var serializer = new JsonDaprSerializer();
        var handler = new WorkflowPayloadHandler(serializer, CreateConverter(store, preference));
        var context = CreateContext(handler);
        object input = referenceInput ? Reference : new Document("document");
        switch (kind)
        {
            case "activity":
                var activity = context.CallActivityAsync<string>("activity", input,
                    new WorkflowTaskOptions(TargetAppId: target, AllowPayloadReferences: allowReferences));
                Assert.False(activity.IsCompleted);
                break;
            case "child":
                var child = context.CallChildWorkflowAsync<string>("child", input,
                    new ChildWorkflowTaskOptions(TargetAppId: target, AllowPayloadReferences: allowReferences));
                Assert.False(child.IsCompleted);
                break;
            case "detached":
                await context.ScheduleNewDetachedWorkflowAsync("detached", null, input, null,
                    new WorkflowTaskOptions(TargetAppId: target, AllowPayloadReferences: allowReferences));
                break;
        }

        var response = new WorkflowResponse();
        response.Actions.AddRange(context.PendingActions);
        var original = Assert.Single(response.Actions);
        var originalWire = InputOf(original);
        Assert.Equal(referenceInput ? Reference.ToEnvelope() : Serialized, originalWire);
        if (target is not null)
        {
            Assert.Equal("source", original.Router.SourceAppID);
            Assert.Equal(target, original.Router.TargetAppID);
        }

        await handler.EncodeResponseAsync(response, context.InstanceId, context.Name, false, token);

        var wire = InputOf(Assert.Single(response.Actions));
        if (offload)
            Assert.NotNull(PayloadReferenceParser.Parse(wire));
        else
            Assert.Equal(referenceInput && !resolve ? Reference.ToEnvelope() : Serialized, wire);
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(PayloadWritePreference.Preserve)]
    [InlineData(PayloadWritePreference.PreferReference)]
    [InlineData(PayloadWritePreference.PreferInline)]
    public async Task InboundAcceptsBothFormatsWithoutRewritingHistory(PayloadWritePreference preference)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        SetupRead(store);
        var serializer = new JsonDaprSerializer();
        var handler = new WorkflowPayloadHandler(serializer, CreateConverter(store, preference));
        var wire = Reference.ToEnvelope();

        Assert.Equal(new Document("document"), await handler.DecodeAsync<Document>(Serialized, Site, token));
        Assert.Equal(new Document("document"), await handler.DecodeAsync<Document>(wire, Site, token));
        Assert.Equal(new Document("document"), await handler.DecodeAsync(wire, typeof(Document), Site, token));
        Assert.Equal(wire, (await handler.DecodeAsync<PayloadReference<Document>>(wire, Site, token))!.ToEnvelope());
        Assert.Equal(wire, Assert.IsType<PayloadReference<Document>>(
            await handler.DecodeAsync(wire, typeof(PayloadReference<Document>), Site, token)).ToEnvelope());
        var prepared = await handler.PrepareAsync(wire, Site, token);
        Assert.Equal(wire, prepared.Wire);
        Assert.Equal(new Document("document"), prepared.ReadAs<Document>(serializer));
        Assert.Equal(wire, prepared.ReadAs<PayloadReference<Document>>(serializer)!.ToEnvelope());
        store.Verify(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()), Times.Exactly(3));
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ReferenceContractsRejectInlineInsteadOfManufacturingReferences()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var serializer = new JsonDaprSerializer();
        var handler = new WorkflowPayloadHandler(serializer, CreateConverter(store, PayloadWritePreference.PreferReference));
        var token = TestContext.Current.CancellationToken;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.DecodeAsync<PayloadReference<Document>>(Serialized, Site, token));
        Assert.Contains("payload is inline", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.DecodeAsync(Serialized, typeof(PayloadReference<Document>), Site, token));
        var prepared = await handler.PrepareAsync(Serialized, Site, token);
        Assert.Throws<InvalidOperationException>(() => prepared.ReadAs<PayloadReference<Document>>(serializer));
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineResolutionChecksDeclaredAndActualSize(bool understatedSize)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        if (understatedSize)
            SetupRead(store);
        var converter = CreateConverter(store, PayloadWritePreference.PreferInline, Bytes.Length - 1);
        var reference = understatedSize ? Reference with { Size = 1 } : Reference;

        await Assert.ThrowsAsync<PayloadRehydrationLimitException>(async () =>
            await converter.EncodeAsync(reference.ToEnvelope(), Site, TestContext.Current.CancellationToken));
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task InlineResolutionAcceptsExactBufferingLimitAndDoesNotReoffload()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        SetupRead(store);
        var converter = CreateConverter(store, PayloadWritePreference.PreferReference, Bytes.Length);

        Assert.Equal(Serialized, await converter.EncodeAsync(Reference.ToEnvelope(),
            Site with { RequireInline = true }, TestContext.Current.CancellationToken));
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task InlineResolutionRejectsCorruptionAndDoesNotFallBack()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        SetupRead(store);
        var converter = CreateConverter(store, PayloadWritePreference.Preserve);

        await Assert.ThrowsAsync<PayloadIntegrityException>(async () =>
            await converter.EncodeAsync((Reference with { ContentHash = "incorrect" }).ToEnvelope(),
                Site with { RequireInline = true }, TestContext.Current.CancellationToken));
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task InlineResolutionSurfacesStorageFailures()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var failure = new InvalidOperationException("Store unavailable");
        store.Setup(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>())).Throws(failure);
        var converter = CreateConverter(store, PayloadWritePreference.PreferInline);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await converter.EncodeAsync(Reference.ToEnvelope(), Site, TestContext.Current.CancellationToken)));
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task InlineResolutionRejectsUnknownVersionsStagesAndCancellation()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var converter = CreateConverter(store, PayloadWritePreference.PreferInline);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await converter.EncodeAsync((Reference with { Version = 99 }).ToEnvelope(), Site, token));
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await converter.EncodeAsync((Reference with { Stages = [new PayloadStage("unknown")] }).ToEnvelope(), Site, token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await converter.EncodeAsync(Reference.ToEnvelope(), Site, new CancellationToken(true)));
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ReferencePermissionIsPerCallAndPreservesRetryAndPropagationOptions()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        SetupRead(store);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(),
            CreateConverter(store, PayloadWritePreference.Preserve));
        var context = CreateContext(handler);
        var options = new WorkflowTaskOptions(TargetAppId: "destination", AllowPayloadReferences: true)
            .WithHistoryPropagation(HistoryPropagationScope.OwnHistory);
        Assert.True((options with { RetryPolicy = null }).AllowPayloadReferences);
        var childOptions = new ChildWorkflowTaskOptions(TargetAppId: "destination", AllowPayloadReferences: true)
            .WithHistoryPropagation(HistoryPropagationScope.OwnHistory);
        Assert.True(childOptions.AllowPayloadReferences);

        _ = context.CallActivityAsync<string>("allowed", Reference, options);
        _ = context.CallActivityAsync<string>("inline", Reference, options with { AllowPayloadReferences = false });
        var response = new WorkflowResponse();
        response.Actions.AddRange(context.PendingActions);
        await handler.EncodeResponseAsync(response, context.InstanceId, context.Name, false,
            TestContext.Current.CancellationToken);
        Assert.Equal(Reference.ToEnvelope(), response.Actions[0].ScheduleTask.Input);
        Assert.Equal(Serialized, response.Actions[1].ScheduleTask.Input);
        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.OwnHistory,
            response.Actions[0].ScheduleTask.HistoryPropagationScope);
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task RecordedCallsAreNotReencodedOnReplay()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(),
            CreateConverter(store, PayloadWritePreference.PreferInline));
        var context = CreateContext(handler);
        _ = context.CallActivityAsync<string>("activity", Reference, new WorkflowTaskOptions(TargetAppId: "destination"));
        context.ProcessEvents([
            new HistoryEvent
            {
                EventId = 0,
                TaskScheduled = new TaskScheduledEvent { Name = "activity", Input = Reference.ToEnvelope() }
            }
        ], isReplaying: true);
        Assert.Empty(context.PendingActions);
        var response = new WorkflowResponse();
        response.Actions.AddRange(context.PendingActions);
        await handler.EncodeResponseAsync(response, context.InstanceId, context.Name, false,
            TestContext.Current.CancellationToken);
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task CrossAppReferenceWithoutPayloadSupportFailsBeforeSending()
    {
        var context = CreateContext(null);
        _ = context.CallActivityAsync<string>("activity", Reference, new WorkflowTaskOptions(TargetAppId: "destination"));
        Assert.Throws<PayloadNotResolvableException>(context.ValidatePendingCallInputs);
        context.ProcessEvents([
            new HistoryEvent
            {
                EventId = 0,
                TaskScheduled = new TaskScheduledEvent { Name = "activity", Input = Reference.ToEnvelope() }
            }
        ], isReplaying: true);
        context.ValidatePendingCallInputs();
        Assert.Empty(context.PendingActions);
        var converter = new DefaultWorkflowPayloadConverter();
        await Assert.ThrowsAsync<PayloadNotResolvableException>(async () =>
            await converter.EncodeAsync(Reference.ToEnvelope(), Site with { RequireInline = true },
                TestContext.Current.CancellationToken));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public void InvalidPreferenceIsRejected()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateConverter(store, (PayloadWritePreference)99));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public void ExistingOptionConstructorsAndDeconstructionRemainAvailable()
    {
        var options = new WorkflowTaskOptions(null, "remote", HistoryPropagationScope.OwnHistory);
        var (retry, app, propagation) = options;
        Assert.Null(retry);
        Assert.Equal("remote", app);
        Assert.Equal(HistoryPropagationScope.OwnHistory, propagation);
        Assert.False(options.AllowPayloadReferences);
        Assert.NotNull(typeof(WorkflowTaskOptions).GetConstructor(
            [typeof(WorkflowRetryPolicy), typeof(string), typeof(HistoryPropagationScope)]));

        var child = new ChildWorkflowTaskOptions("child", null, "remote", HistoryPropagationScope.Lineage);
        var (instanceId, childRetry, childApp, childPropagation) = child;
        Assert.Equal("child", instanceId);
        Assert.Null(childRetry);
        Assert.Equal("remote", childApp);
        Assert.Equal(HistoryPropagationScope.Lineage, childPropagation);
        Assert.False(child.AllowPayloadReferences);
        Assert.NotNull(typeof(ChildWorkflowTaskOptions).GetConstructor(
            [typeof(string), typeof(WorkflowRetryPolicy), typeof(string), typeof(HistoryPropagationScope)]));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task TargetAppIdRequiresInliningEvenWithoutSourceAppId()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        SetupRead(store);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(),
            CreateConverter(store, PayloadWritePreference.PreferReference));
        var context = new WorkflowOrchestrationContext("workflow", "instance", DateTime.UtcNow,
            new JsonDaprSerializer(), NullLoggerFactory.Instance, new WorkflowVersionTracker([]), payloads: handler);
        _ = context.CallActivityAsync<long>("activity", Reference, new WorkflowTaskOptions(TargetAppId: "destination"));
        var response = new WorkflowResponse();
        response.Actions.AddRange(context.PendingActions);
        await handler.EncodeResponseAsync(response, context.InstanceId, context.Name, false,
            TestContext.Current.CancellationToken);
        Assert.Equal(Serialized, response.Actions[0].ScheduleTask.Input);
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task DetachedOptionsRejectUnsupportedSettings()
    {
        var context = CreateContext(null);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.ScheduleNewDetachedWorkflowAsync("workflow", null, null, null, null!));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.ScheduleNewDetachedWorkflowAsync("workflow", null, null, null,
                new WorkflowTaskOptions(PropagationScope: HistoryPropagationScope.OwnHistory)));
        Assert.Empty(context.PendingActions);
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(PayloadWritePreference.Preserve, false, PayloadKind.All)]
    [InlineData(PayloadWritePreference.Preserve, true, PayloadKind.All)]
    [InlineData(PayloadWritePreference.PreferReference, false, PayloadKind.All)]
    [InlineData(PayloadWritePreference.PreferReference, true, PayloadKind.All)]
    [InlineData(PayloadWritePreference.PreferReference, false, PayloadKind.ActivityInput)]
    [InlineData(PayloadWritePreference.PreferInline, false, PayloadKind.None)]
    [InlineData(PayloadWritePreference.PreferInline, true, PayloadKind.None)]
    public async Task ClientAndWorkflowEventsFollowWritePreference(
        PayloadWritePreference preference, bool referenceInput, PayloadKind sites)
    {
        var token = TestContext.Current.CancellationToken;
        var offload = !referenceInput && preference == PayloadWritePreference.PreferReference &&
                      sites.HasFlag(PayloadKind.ExternalEvent);
        var resolve = referenceInput && preference == PayloadWritePreference.PreferInline;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        if (resolve)
            SetupRead(store);
        if (offload)
        {
            store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                    DaprBinaryStoreClient.DefaultChunkSize, token))
                .Returns(Task.CompletedTask);
        }
        var serializer = new JsonDaprSerializer();
        var codec = CreateConverter(store, preference, sites: sites);
        var handler = new WorkflowPayloadHandler(serializer, codec);
        var context = CreateContext(handler);
        object input = referenceInput ? Reference : new Document("document");
        context.SendEvent("recipient", "event", input);
        var response = new WorkflowResponse();
        response.Actions.AddRange(context.PendingActions);
        await handler.EncodeResponseAsync(response, context.InstanceId, context.Name, false, token);
        var workflowWire = Assert.Single(response.Actions).SendEvent.Data;

        var grpc = new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(
            MockBehavior.Strict, new Mock<CallInvoker>().Object);
        RaiseEventRequest? request = null;
        grpc.Setup(c => c.RaiseEventAsync(It.IsAny<RaiseEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<RaiseEventRequest, CallOptions>((value, options) =>
            {
                request = value;
                Assert.Equal(token, options.CancellationToken);
            })
            .Returns(new AsyncUnaryCall<RaiseEventResponse>(Task.FromResult(new RaiseEventResponse()),
                Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { }));
        var client = new WorkflowGrpcClient(grpc.Object, NullLogger<WorkflowGrpcClient>.Instance,
            serializer, payloadConverter: codec);
        await client.RaiseEventAsync("recipient", "event", input, token);

        Assert.NotNull(request);
        Assert.Equal("recipient", request.InstanceId);
        Assert.Equal("event", request.Name);
        Assert.Equal(workflowWire, request.Input);
        if (offload)
        {
            var reference = Assert.IsType<PayloadReference>(PayloadReferenceParser.Parse(workflowWire));
            Assert.StartsWith("workflow-payloads/recipient/", reference.Key);
            Assert.Equal(Bytes.Length, reference.Size);
            Assert.Equal(Reference.ContentHash, reference.ContentHash);
        }
        else
            Assert.Equal(referenceInput && !resolve ? Reference.ToEnvelope() : Serialized, workflowWire);
        store.VerifyAll();
        store.VerifyNoOtherCalls();
        grpc.VerifyAll();
        grpc.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InboundEventsAcceptLiveAndBufferedPayloads(bool referenceInput, bool buffered)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        if (referenceInput)
            SetupRead(store);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(),
            CreateConverter(store, PayloadWritePreference.PreferInline));
        var context = CreateContext(handler);
        var history = new HistoryEvent
        {
            EventRaised = new EventRaisedEvent
            {
                Name = "event", Input = referenceInput ? Reference.ToEnvelope() : Serialized
            }
        };
        var original = history.Clone();
        if (buffered)
            context.ProcessEvents([history], isReplaying: true);
        var task = context.WaitForExternalEventAsync<Document>("event", token);
        if (!buffered)
            context.ProcessEvents([history], isReplaying: false);
        await handler.DrainAsync();

        Assert.Equal(new Document("document"), await task);
        Assert.Equal(original, history);
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReferenceTypedEventsDoNotResolveOrManufactureReferences(bool referenceInput)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(),
            CreateConverter(store, PayloadWritePreference.PreferReference));
        var context = CreateContext(handler);
        var task = context.WaitForExternalEventAsync<PayloadReference<Document>>("event",
            TestContext.Current.CancellationToken);
        context.ProcessEvents([
            new HistoryEvent
            {
                EventRaised = new EventRaisedEvent
                {
                    Name = "event", Input = referenceInput ? Reference.ToEnvelope() : Serialized
                }
            }
        ], isReplaying: false);
        await handler.DrainAsync();

        if (referenceInput)
            Assert.Equal(Reference.ToEnvelope(), (await task).ToEnvelope());
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventInliningFailureDisposesTheStreamAndNeverSends(bool cancelDuringRead)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var disposed = false;
        var failure = new IOException("Stream interrupted after its first chunk");
        async IAsyncEnumerable<ReadOnlyMemory<byte>> FailingChunks()
        {
            try
            {
                await Task.Yield();
                yield return Bytes.AsMemory(0, 4);
                if (cancelDuringRead)
                {
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                }
                throw failure;
            }
            finally
            {
                disposed = true;
            }
        }
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.GetBinaryFileChunksAsync("store", "key", cancellation.Token))
            .Returns(FailingChunks);
        var grpc = new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(
            MockBehavior.Strict, new Mock<CallInvoker>().Object);
        var client = new WorkflowGrpcClient(grpc.Object, NullLogger<WorkflowGrpcClient>.Instance,
            new JsonDaprSerializer(), payloadConverter: CreateConverter(store, PayloadWritePreference.PreferInline));
        if (cancelDuringRead)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.RaiseEventAsync("recipient", "event", Reference, cancellation.Token));
        else
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
                client.RaiseEventAsync("recipient", "event", Reference, cancellation.Token)));
        Assert.True(disposed);
        grpc.VerifyNoOtherCalls();
        store.VerifyAll();
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RetriesRetainCrossAppReferencePermission(bool child, bool allowReferences)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        if (!allowReferences)
            SetupRead(store);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(),
            CreateConverter(store, PayloadWritePreference.Preserve));
        var context = CreateContext(handler);
        var retry = new WorkflowRetryPolicy(2, TimeSpan.FromSeconds(1));
        var task = child
            ? context.CallChildWorkflowAsync<string>("work", Reference,
                new ChildWorkflowTaskOptions(RetryPolicy: retry, TargetAppId: "destination",
                    AllowPayloadReferences: allowReferences))
            : context.CallActivityAsync<string>("work", Reference,
                new WorkflowTaskOptions(RetryPolicy: retry, TargetAppId: "destination",
                    AllowPayloadReferences: allowReferences));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var action = Assert.Single(context.PendingActions);
            var response = new WorkflowResponse { Actions = { action } };
            await handler.EncodeResponseAsync(response, context.InstanceId, context.Name, false,
                TestContext.Current.CancellationToken);
            Assert.Equal(allowReferences ? Reference.ToEnvelope() : Serialized, InputOf(action));
            Assert.Equal("destination", action.Router.TargetAppID);
            var scheduled = new HistoryEvent { EventId = action.Id };
            if (child)
                scheduled.ChildWorkflowInstanceCreated = new() { Name = "work" };
            else
                scheduled.TaskScheduled = new() { Name = "work" };
            var completion = new HistoryEvent();
            var failure = new TaskFailureDetails { ErrorType = "Transient", ErrorMessage = "retry" };
            if (attempt == 0)
            {
                if (child)
                    completion.ChildWorkflowInstanceFailed = new() { TaskScheduledId = action.Id, FailureDetails = failure };
                else
                    completion.TaskFailed = new() { TaskScheduledId = action.Id, FailureDetails = failure };
            }
            else if (child)
                completion.ChildWorkflowInstanceCompleted = new() { TaskScheduledId = action.Id, Result = "\"ok\"" };
            else
                completion.TaskCompleted = new() { TaskScheduledId = action.Id, Result = "\"ok\"" };
            context.ProcessEvents([scheduled, completion], isReplaying: true);
            await handler.DrainAsync();
            if (attempt == 0)
            {
                var timer = Assert.Single(context.PendingActions);
                Assert.NotNull(timer.CreateTimer);
                context.ProcessEvents([
                    new HistoryEvent { EventId = timer.Id, TimerCreated = new TimerCreatedEvent() },
                    new HistoryEvent { TimerFired = new TimerFiredEvent { TimerId = timer.Id } }
                ], isReplaying: true);
            }
        }
        Assert.Equal("ok", await task);
        Assert.Empty(context.PendingActions);
        if (!allowReferences)
            store.Verify(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.VerifyNoOtherCalls();
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task CrossAppInliningDoesNotRecursivelyChangeNestedReferenceContracts()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var serializer = new JsonDaprSerializer();
        var handler = new WorkflowPayloadHandler(serializer, CreateConverter(store, PayloadWritePreference.PreferInline));
        var context = CreateContext(handler);
        var input = new { Document = Reference };
        _ = context.CallActivityAsync<string>("activity", input, new WorkflowTaskOptions(TargetAppId: "destination"));
        var response = new WorkflowResponse();
        response.Actions.AddRange(context.PendingActions);
        await handler.EncodeResponseAsync(response, context.InstanceId, context.Name, false,
            TestContext.Current.CancellationToken);
        Assert.Equal(serializer.Serialize(input), Assert.Single(response.Actions).ScheduleTask.Input);
        store.VerifyNoOtherCalls();
    }

    private static string InputOf(WorkflowAction action) =>
        action.ScheduleTask?.Input ?? action.CreateChildWorkflow?.Input ?? action.CreateDetachedWorkflow!.Input;

    private static WorkflowOrchestrationContext CreateContext(WorkflowPayloadHandler? handler) =>
        new("workflow", "instance", DateTime.UtcNow, new JsonDaprSerializer(), NullLoggerFactory.Instance,
            new WorkflowVersionTracker([]), appId: "source", payloads: handler);

    private static BinaryStorePayloadConverter CreateConverter(Mock<DaprBinaryStoreClient> store,
        PayloadWritePreference preference, long limit = 16 * 1024 * 1024, PayloadKind sites = PayloadKind.All) =>
        new(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store", WritePreference = preference, OffloadThreshold = null,
            MaxInMemoryRehydrationSize = limit, OffloadSites = sites
        }), NullWorkflowPayloadManifest.Instance);

    private static void SetupRead(Mock<DaprBinaryStoreClient> store) =>
        store.Setup(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()))
            .Returns(() => ReadChunks());

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks()
    {
        await Task.Yield();
        yield return Bytes.AsMemory(0, Bytes.Length / 2);
        yield return Bytes.AsMemory(Bytes.Length / 2);
    }

    private sealed record Document(string Content);
}
