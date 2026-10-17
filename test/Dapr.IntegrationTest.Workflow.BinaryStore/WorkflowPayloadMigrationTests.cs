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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Payloads;
using Dapr.Workflow.Versioning;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowGrpc = Dapr.DurableTask.Protobuf;

namespace Dapr.IntegrationTest.Workflow.BinaryStore;

public sealed class WorkflowPayloadMigrationTests
{
    private const string FamilyName = "PayloadMigration";
    private const string PatchWorkflowName = "PatchedPayloadMigration";
    private const string ReferencePatch = "use-payload-references";
    private static readonly Document Input = new("input", new string('x', 128 * 1024));

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(PayloadMode.Inline, PayloadMode.Inline)]
    [InlineData(PayloadMode.Inline, PayloadMode.Automatic)]
    [InlineData(PayloadMode.Automatic, PayloadMode.ReadOnly)]
    [InlineData(PayloadMode.Automatic, PayloadMode.PreferInline)]
    public async Task ShouldReplayExistingHistoryAfterChangingPayloadMode(PayloadMode before, PayloadMode after)
    {
        using var timeout = CreateTimeout();
        var token = timeout.Token;
        await using var deployment = await Deployment.CreateAsync(token);
        await deployment.StartWorkerAsync(before, token);
        var instanceId = Guid.NewGuid().ToString("N");

        await deployment.Client.ScheduleNewWorkflowAsync(nameof(MigrationWorkflow), instanceId, Input,
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(instanceId, "waiting-1", token);
        var originalHistory = await deployment.GetHistoryAsync(instanceId, token);
        var firstInput = Assert.Single(originalHistory.Events, e => e.ExecutionStarted is not null)
            .ExecutionStarted.Input;
        await deployment.AssertWireAsync(firstInput, Input, before == PayloadMode.Automatic, token);
        Assert.Equal(1, deployment.Calls.Count(instanceId, nameof(EchoActivity)));

        await deployment.StartWorkerAsync(after, token);
        var firstEvent = Input with { Name = "event-1" };
        await deployment.Client.RaiseEventAsync(instanceId, "resume-1", firstEvent, token);
        await deployment.WaitForCheckpointAsync(instanceId, "waiting-2", token);
        var mixedHistory = await deployment.GetHistoryAsync(instanceId, token);
        Assert.Equal(originalHistory.Events, mixedHistory.Events.Take(originalHistory.Events.Count));
        Assert.Equal(2, deployment.Calls.Count(instanceId, nameof(EchoActivity)));

        // A second fresh worker must replay both formats, not just write a new format successfully.
        await deployment.StartWorkerAsync(after, token);
        var secondEvent = Input with { Name = "event-2" };
        await deployment.Client.RaiseEventAsync(instanceId, "resume-2", secondEvent, token);
        var result = await deployment.Client.WaitForWorkflowCompletionAsync(instanceId, true, token);
        var expected = Input with { Name = "input:echo:event-1:echo:event-2:echo" };
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(Input, result.ReadInputAs<Document>());
        Assert.Equal(expected, result.ReadOutputAs<Document>());
        Assert.Equal(3, deployment.Calls.Count(instanceId, nameof(EchoActivity)));

        var history = await deployment.GetHistoryAsync(instanceId, token);
        Assert.Equal(mixedHistory.Events, history.Events.Take(mixedHistory.Events.Count));
        var scheduled = history.Events.Where(e => e.TaskScheduled is not null).ToArray();
        var completed = history.Events.Where(e => e.TaskCompleted is not null).ToArray();
        Assert.Equal(3, scheduled.Length);
        Assert.Equal(3, completed.Length);
        var activityInputs = new[]
        {
            Input, Input with { Name = "input:echo:event-1" },
            Input with { Name = "input:echo:event-1:echo:event-2" }
        };
        for (var i = 0; i < scheduled.Length; i++)
        {
            var offloaded = (i == 0 ? before : after) == PayloadMode.Automatic;
            Assert.Equal(nameof(EchoActivity), scheduled[i].TaskScheduled.Name);
            await deployment.AssertWireAsync(scheduled[i].TaskScheduled.Input, activityInputs[i], offloaded, token);
            var completion = Assert.Single(completed, e => e.TaskCompleted.TaskScheduledId == scheduled[i].EventId);
            await deployment.AssertWireAsync(completion.TaskCompleted.Result,
                activityInputs[i] with { Name = activityInputs[i].Name + ":echo" }, offloaded, token);
        }

        var events = history.Events.Where(e => e.EventRaised is not null).ToArray();
        Assert.Equal(2, events.Length);
        await deployment.AssertWireAsync(events[0].EventRaised.Input, firstEvent, after == PayloadMode.Automatic,
            token);
        await deployment.AssertWireAsync(events[1].EventRaised.Input, secondEvent, after == PayloadMode.Automatic,
            token);
        var persisted = await deployment.GetInstanceAsync(instanceId, token);
        Assert.Equal(firstInput, persisted.WorkflowState.Input);
        await deployment.AssertWireAsync(persisted.WorkflowState.Output, expected, after == PayloadMode.Automatic,
            token);
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(PayloadMode.ReadOnly)]
    [InlineData(PayloadMode.Automatic)]
    [InlineData(PayloadMode.PreferInline)]
    public async Task ShouldKeepMultipleDocumentReferencesDistinctAcrossReplayAndPolicyChanges(PayloadMode after)
    {
        using var timeout = CreateTimeout();
        var token = timeout.Token;
        await using var deployment = await Deployment.CreateAsync(token);
        await deployment.StartWorkerAsync(PayloadMode.Automatic, token);
        var instanceId = Guid.NewGuid().ToString("N");
        Document[] documents =
        [
            new("first", new string('a', 128 * 1024)),
            new("second", new string('b', 160 * 1024)),
            new("third", new string('c', 192 * 1024))
        ];
        var resolver = deployment.Services.GetRequiredService<IWorkflowPayloadResolver>();
        var references = await Task.WhenAll(documents.Select(document =>
            resolver.StoreAsync(document, instanceId, token)));
        Assert.Equal(documents.Length, references.Select(reference => reference.Key).Distinct().Count());
        Assert.All(references, reference =>
            Assert.True(reference.Size > new BinaryStorePayloadOptions().OffloadThreshold));

        await deployment.Client.ScheduleNewWorkflowAsync(nameof(MultipleDocumentWorkflow), instanceId, references,
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(instanceId, "waiting", token);
        var original = await deployment.GetHistoryAsync(instanceId, token);
        Assert.Equal(documents.Length, original.Events.Count(e => e.TaskCompleted is not null));
        Assert.Equal(documents.Length, deployment.Calls.Count(instanceId, nameof(EchoActivity)));

        await deployment.StartWorkerAsync(after, token);
        await deployment.Client.RaiseEventAsync(instanceId, "resume", true, token);
        var result = await deployment.Client.WaitForWorkflowCompletionAsync(instanceId, true, token);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var expected = documents.Select(document => document with { Name = document.Name + ":echo:echo" }).ToArray();
        Assert.Equal(expected, result.ReadOutputAs<Document[]>());
        Assert.Equal(references.Select(reference => reference.ToEnvelope()),
            result.ReadInputAs<PayloadReference<Document>[]>()!.Select(reference => reference.ToEnvelope()));
        Assert.Equal(documents.Length * 2, deployment.Calls.Count(instanceId, nameof(EchoActivity)));

        var history = await deployment.GetHistoryAsync(instanceId, token);
        Assert.Equal(original.Events, history.Events.Take(original.Events.Count));
        var scheduled = history.Events.Where(e => e.TaskScheduled is not null).ToArray();
        var completed = history.Events.Where(e => e.TaskCompleted is not null).ToArray();
        Assert.Equal(documents.Length * 2, scheduled.Length);
        Assert.Equal(scheduled.Length, completed.Length);
        var firstStageKeys = new HashSet<string>();
        for (var i = 0; i < scheduled.Length; i++)
        {
            var firstStage = i < documents.Length;
            var document = documents[i % documents.Length];
            var input = firstStage ? document : document with { Name = document.Name + ":echo" };
            var task = scheduled[i];
            Assert.Equal(nameof(EchoActivity), task.TaskScheduled.Name);
            await deployment.AssertWireAsync(task.TaskScheduled.Input, input,
                firstStage || after != PayloadMode.PreferInline, token);
            var completion = Assert.Single(completed, e => e.TaskCompleted.TaskScheduledId == task.EventId);
            await deployment.AssertWireAsync(completion.TaskCompleted.Result,
                input with { Name = input.Name + ":echo" }, firstStage || after == PayloadMode.Automatic, token);

            if (firstStage)
            {
                Assert.Equal(references[i].ToEnvelope(), task.TaskScheduled.Input);
                Assert.True(PayloadReference.TryParse(completion.TaskCompleted.Result, out var reference));
                Assert.NotNull(reference);
                Assert.StartsWith($"workflow-payloads/{instanceId}/", reference.Key);
                Assert.True(firstStageKeys.Add(reference.Key), "Different documents must not share an output key.");
                Assert.DoesNotContain(reference.Key, references.Select(value => value.Key));
                if (after != PayloadMode.PreferInline)
                    Assert.Equal(completion.TaskCompleted.Result, scheduled[i + documents.Length].TaskScheduled.Input);
            }
        }

        var persisted = await deployment.GetInstanceAsync(instanceId, token);
        Assert.Equal(Assert.Single(original.Events, e => e.ExecutionStarted is not null).ExecutionStarted.Input,
            persisted.WorkflowState.Input);
        await deployment.AssertWireAsync(persisted.WorkflowState.Output, expected, after == PayloadMode.Automatic, token);
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(PayloadMode.ReadOnly)]
    [InlineData(PayloadMode.Automatic)]
    [InlineData(PayloadMode.PreferInline)]
    public async Task ShouldApplyClientEventPoliciesAndReplayOriginalWireValues(PayloadMode mode)
    {
        using var timeout = CreateTimeout();
        var token = timeout.Token;
        await using var deployment = await Deployment.CreateAsync(token);
        await deployment.StartWorkerAsync(mode, token);
        var receiverId = Guid.NewGuid().ToString("N");
        var reference = await deployment.Services.GetRequiredService<IWorkflowPayloadResolver>()
            .StoreAsync(Input, receiverId, token);
        await deployment.Client.ScheduleNewWorkflowAsync(nameof(EventReceiverWorkflow), receiverId, input: null,
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(receiverId, "waiting", token);
        await deployment.Client.RaiseEventAsync(receiverId, "inline", Input, token);
        await deployment.Client.RaiseEventAsync(receiverId, "reference", reference, token);
        await deployment.WaitForCheckpointAsync(receiverId, "received", token);
        var original = await deployment.GetHistoryAsync(receiverId, token);
        var raised = original.Events.Where(e => e.EventRaised is not null).Select(e => e.EventRaised).ToArray();
        Assert.Equal(2, raised.Length);
        await deployment.AssertWireAsync(Assert.Single(raised, e => e.Name == "inline").Input,
            Input, mode == PayloadMode.Automatic, token);
        var referenceWire = Assert.Single(raised, e => e.Name == "reference").Input;
        Assert.Equal(mode == PayloadMode.PreferInline ? deployment.Serializer.Serialize(Input) : reference.ToEnvelope(),
            referenceWire);

        await deployment.StartWorkerAsync(mode, token);
        await deployment.Client.RaiseEventAsync(receiverId, "finish", true, token);
        var completed = await deployment.Client.WaitForWorkflowCompletionAsync(receiverId, true, token);
        Assert.Equal(WorkflowRuntimeStatus.Completed, completed.RuntimeStatus);
        Assert.Equal("input|input", completed.ReadOutputAs<string>());
        var replayed = await deployment.GetHistoryAsync(receiverId, token);
        Assert.Equal(original.Events, replayed.Events.Take(original.Events.Count));
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(PayloadMode.ReadOnly)]
    [InlineData(PayloadMode.Automatic)]
    public async Task ShouldPreserveVersionContractsWhileNewVersionUsesExplicitReferences(PayloadMode mode)
    {
        using var timeout = CreateTimeout();
        var token = timeout.Token;
        await using var deployment = await Deployment.CreateAsync(token);
        await deployment.StartWorkerAsync(PayloadMode.Inline, token, versioned: true, selectV1: true);
        Assert.Equal(nameof(PayloadMigrationV1), deployment.LatestVersion);
        var oldId = Guid.NewGuid().ToString("N");
        await deployment.Client.ScheduleNewWorkflowAsync(deployment.LatestVersion, oldId, Input,
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(oldId, "waiting", token);
        var originalHistory = await deployment.GetHistoryAsync(oldId, token);

        await deployment.StartWorkerAsync(mode, token, versioned: true);
        Assert.Equal(nameof(PayloadMigrationV2), deployment.LatestVersion);
        var newId = Guid.NewGuid().ToString("N");
        var reference = await deployment.Services.GetRequiredService<IWorkflowPayloadResolver>()
            .StoreAsync(Input, newId, token);
        await deployment.Client.ScheduleNewWorkflowAsync(deployment.LatestVersion, newId, reference.AsTyped<Document>(),
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(newId, "waiting", token);

        await deployment.StartWorkerAsync(mode, token, versioned: true);
        await deployment.Client.RaiseEventAsync(oldId, "resume", true, token);
        await deployment.Client.RaiseEventAsync(newId, "resume", true, token);
        var oldResult = await deployment.Client.WaitForWorkflowCompletionAsync(oldId, true, token);
        var newResult = await deployment.Client.WaitForWorkflowCompletionAsync(newId, true, token);
        Assert.Equal(WorkflowRuntimeStatus.Completed, oldResult.RuntimeStatus);
        Assert.Equal(WorkflowRuntimeStatus.Completed, newResult.RuntimeStatus);
        Assert.Equal(Input with { Name = "input:echo" }, oldResult.ReadOutputAs<Document>());
        Assert.Equal(reference.Size, newResult.ReadOutputAs<long>());

        var oldHistory = await deployment.GetHistoryAsync(oldId, token);
        Assert.Equal(originalHistory.Events, oldHistory.Events.Take(originalHistory.Events.Count));
        Assert.Equal(nameof(PayloadMigrationV1),
            Assert.Single(oldHistory.Events, e => e.ExecutionStarted is not null).ExecutionStarted.Name);
        var oldTask = Assert.Single(oldHistory.Events, e => e.TaskScheduled is not null).TaskScheduled;
        Assert.Equal(nameof(EchoActivity), oldTask.Name);
        await deployment.AssertWireAsync(oldTask.Input, Input, false, token);
        var oldState = await deployment.GetInstanceAsync(oldId, token);
        await deployment.AssertWireAsync(oldState.WorkflowState.Input, Input, false, token);
        await deployment.AssertWireAsync(oldState.WorkflowState.Output, Input with { Name = "input:echo" },
            mode == PayloadMode.Automatic, token);

        var newHistory = await deployment.GetHistoryAsync(newId, token);
        var started = Assert.Single(newHistory.Events, e => e.ExecutionStarted is not null).ExecutionStarted;
        Assert.Equal(nameof(PayloadMigrationV2), started.Name);
        Assert.Equal(reference.ToEnvelope(), started.Input);
        var newTask = Assert.Single(newHistory.Events, e => e.TaskScheduled is not null).TaskScheduled;
        Assert.Equal(nameof(StreamActivity), newTask.Name);
        Assert.Equal(reference.ToEnvelope(), newTask.Input);
        Assert.Equal(1, deployment.Calls.Count(oldId, nameof(EchoActivity)));
        Assert.Equal(0, deployment.Calls.Count(oldId, nameof(StreamActivity)));
        Assert.Equal(0, deployment.Calls.Count(newId, nameof(EchoActivity)));
        Assert.Equal(1, deployment.Calls.Count(newId, nameof(StreamActivity)));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldPatchInternalReferenceActivitiesWithoutChangingExistingInputContract()
    {
        using var timeout = CreateTimeout();
        var token = timeout.Token;
        await using var deployment = await Deployment.CreateAsync(token);
        await deployment.StartWorkerAsync(PayloadMode.Inline, token);
        var oldId = Guid.NewGuid().ToString("N");
        await deployment.Client.ScheduleNewWorkflowAsync(PatchWorkflowName, oldId, Input,
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(oldId, "waiting", token);
        var originalHistory = await deployment.GetHistoryAsync(oldId, token);

        await deployment.StartWorkerAsync(PayloadMode.ReadOnly, token, patched: true);
        var newId = Guid.NewGuid().ToString("N");
        await deployment.Client.ScheduleNewWorkflowAsync(PatchWorkflowName, newId, Input,
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(newId, "waiting", token);

        await deployment.StartWorkerAsync(PayloadMode.ReadOnly, token, patched: true);
        foreach (var instanceId in new[] { oldId, newId })
        {
            await deployment.Client.RaiseEventAsync(instanceId, "resume", true, token);
            var result = await deployment.Client.WaitForWorkflowCompletionAsync(instanceId, true, token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal(Input, result.ReadInputAs<Document>());
            Assert.Equal(Encoding.UTF8.GetByteCount(deployment.Serializer.Serialize(Input)),
                result.ReadOutputAs<long>());
        }

        var oldHistory = await deployment.GetHistoryAsync(oldId, token);
        Assert.Equal(originalHistory.Events, oldHistory.Events.Take(originalHistory.Events.Count));
        Assert.Equal([nameof(MeasureActivity)],
            oldHistory.Events.Where(e => e.TaskScheduled is not null).Select(e => e.TaskScheduled.Name));
        var newHistory = await deployment.GetHistoryAsync(newId, token);
        Assert.Equal([nameof(StoreActivity), nameof(StreamActivity)],
            newHistory.Events.Where(e => e.TaskScheduled is not null).Select(e => e.TaskScheduled.Name));
        var stored = Assert.Single(newHistory.Events, e => e.TaskCompleted is not null &&
                                                           PayloadReference.TryParse(e.TaskCompleted.Result, out _))
            .TaskCompleted.Result;
        var streamed = Assert.Single(newHistory.Events, e => e.TaskScheduled?.Name == nameof(StreamActivity));
        Assert.Equal(stored, streamed.TaskScheduled.Input);
        Assert.Contains(newHistory.Events, e => e.WorkflowStarted?.Version?.Patches.Contains(ReferencePatch) == true);
        Assert.DoesNotContain(oldHistory.Events,
            e => e.WorkflowStarted?.Version?.Patches.Contains(ReferencePatch) == true);
        Assert.Equal(1, deployment.Calls.Count(oldId, nameof(MeasureActivity)));
        Assert.Equal(0, deployment.Calls.Count(oldId, nameof(StoreActivity)));
        Assert.Equal(0, deployment.Calls.Count(newId, nameof(MeasureActivity)));
        Assert.Equal(1, deployment.Calls.Count(newId, nameof(StoreActivity)));
        Assert.Equal(1, deployment.Calls.Count(newId, nameof(StreamActivity)));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldRejectAnIncompatibleInputTypeBeforeReachingThePatch()
    {
        using var timeout = CreateTimeout();
        var token = timeout.Token;
        await using var deployment = await Deployment.CreateAsync(token);
        await deployment.StartWorkerAsync(PayloadMode.Inline, token);
        var instanceId = Guid.NewGuid().ToString("N");
        await deployment.Client.ScheduleNewWorkflowAsync(PatchWorkflowName, instanceId, Input,
            startTime: null, cancellation: token);
        await deployment.WaitForCheckpointAsync(instanceId, "waiting", token);

        await deployment.StartWorkerAsync(PayloadMode.ReadOnly, token, incompatibleInput: true);
        await deployment.Client.RaiseEventAsync(instanceId, "resume", true, token);
        var result = await deployment.Client.WaitForWorkflowCompletionAsync(instanceId, true, token);
        Assert.Equal(WorkflowRuntimeStatus.Failed, result.RuntimeStatus);
        Assert.NotNull(result.FailureDetails);
        Assert.Contains("payload is inline", result.FailureDetails.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, deployment.Calls.Count(instanceId, "patch-body"));
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        return timeout;
    }

    public enum PayloadMode { Inline, Automatic, ReadOnly, PreferInline }

    internal sealed record Document(string Name, string Content);

    internal sealed class ActivityCalls
    {
        private readonly ConcurrentDictionary<(string InstanceId, string Activity), int> _counts = new();

        public void Record(string instanceId, string activity) =>
            _counts.AddOrUpdate((instanceId, activity), 1, (_, count) => count + 1);

        public int Count(string instanceId, string activity) => _counts.GetValueOrDefault((instanceId, activity));
    }

    private sealed class Deployment(DaprTestEnvironment environment, WorkflowHarness harness) : IAsyncDisposable
    {
        private WebApplication? _worker;
        public ActivityCalls Calls { get; } = new();

        public IServiceProvider Services =>
            (_worker ?? throw new InvalidOperationException("No worker is running.")).Services;

        public DaprWorkflowClient Client => Services.GetRequiredService<DaprWorkflowClient>();
        public IDaprSerializer Serializer => Services.GetRequiredService<IDaprSerializer>();

        private WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient Grpc =>
            Services.GetRequiredService<WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient>();

        public string LatestVersion
        {
            get
            {
                var name = GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(Services)[FamilyName][0];
                return name[(name.LastIndexOf('.') + 1)..];
            }
        }

        public static async Task<Deployment> CreateAsync(CancellationToken token)
        {
            var directory = TestDirectoryManager.CreateTestDirectory("workflow-payload-migration");
            InMemoryBinaryStoreContainer.Yaml.WriteBinaryStoreYamlToFolder(directory);
            var options = new DaprRuntimeOptions();
            var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(options,
                needsActorState: true, cancellationToken: token);
            var harness = new DaprHarnessBuilder(directory).WithOptions(options).WithEnvironment(environment)
                .BuildWorkflow();
            var deployment = new Deployment(environment, harness);
            try
            {
                await harness.InitializeAsync(token);
                return deployment;
            }
            catch
            {
                await deployment.DisposeAsync();
                throw;
            }
        }

        public async Task StartWorkerAsync(PayloadMode mode, CancellationToken token, bool versioned = false,
            bool selectV1 = false, bool patched = false, bool incompatibleInput = false)
        {
            // Keep the sidecar alive: restarting an in-memory binary store would destroy the payloads.
            await StopWorkerAsync();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{harness.AppPort}");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_GRPC_ENDPOINT"] = $"http://127.0.0.1:{harness.DaprGrpcPort}",
                ["DAPR_HTTP_ENDPOINT"] = $"http://127.0.0.1:{harness.DaprHttpPort}"
            });
            builder.Services.AddSingleton(Calls);
            if (versioned)
            {
                builder.Services.AddDaprWorkflowVersioning(options =>
                {
                    if (selectV1)
                        options.DefaultSelector = _ => new FirstVersionSelector();
                });
            }

            var workflowBuilder = builder.Services.AddDaprWorkflowBuilder(
                configureRuntime: runtime =>
                {
                    runtime.RegisterWorkflow<MigrationWorkflow>();
                    runtime.RegisterWorkflow<MultipleDocumentWorkflow>();
                    runtime.RegisterWorkflow<EventReceiverWorkflow>();
                    runtime.RegisterActivity<EchoActivity>();
                    runtime.RegisterActivity<MeasureActivity>();
                    runtime.RegisterActivity<StoreActivity>();
                    runtime.RegisterActivity<StreamActivity>();
                    if (incompatibleInput)
                        runtime.RegisterWorkflow<IncompatibleInputWorkflow>(PatchWorkflowName);
                    else if (patched)
                        runtime.RegisterWorkflow<PatchedWorkflow>(PatchWorkflowName);
                    else
                        runtime.RegisterWorkflow<OriginalWorkflow>(PatchWorkflowName);
                },
                configureClient: (_, client) => client.UseGrpcEndpoint($"http://127.0.0.1:{harness.DaprGrpcPort}"));
            if (mode != PayloadMode.Inline)
            {
                workflowBuilder.WithBinaryStorePayloads(options =>
                {
                    options.StoreName = Constants.DaprComponentNames.BinaryStoreComponentName;
                    options.WritePreference = mode switch
                    {
                        PayloadMode.Automatic => PayloadWritePreference.PreferReference,
                        PayloadMode.PreferInline => PayloadWritePreference.PreferInline,
                        _ => PayloadWritePreference.Preserve
                    };
                });
            }

            _worker = builder.Build();
            await _worker.StartAsync(token);
        }

        public async Task WaitForCheckpointAsync(string instanceId, string checkpoint, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var state = await GetInstanceAsync(instanceId, token);
                if (state.Exists)
                {
                    Assert.NotNull(state.WorkflowState);
                    Assert.True(state.WorkflowState.WorkflowStatus is
                            WorkflowGrpc.OrchestrationStatus.Pending or WorkflowGrpc.OrchestrationStatus.Running,
                        $"Workflow '{instanceId}' ended before checkpoint '{checkpoint}': " +
                        $"{state.WorkflowState.WorkflowStatus}, {state.WorkflowState.FailureDetails}");
                    if (state.WorkflowState.CustomStatus == Serializer.Serialize(checkpoint))
                        return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100), token);
            }
        }

        public async Task<WorkflowGrpc.GetInstanceResponse>
            GetInstanceAsync(string instanceId, CancellationToken token) =>
            await Grpc.GetInstanceAsync(
                new WorkflowGrpc.GetInstanceRequest { InstanceId = instanceId, GetInputsAndOutputs = true },
                new CallOptions(cancellationToken: token));

        public async Task<WorkflowGrpc.GetInstanceHistoryResponse> GetHistoryAsync(string instanceId,
            CancellationToken token) =>
            await Grpc.GetInstanceHistoryAsync(new WorkflowGrpc.GetInstanceHistoryRequest { InstanceId = instanceId },
                new CallOptions(cancellationToken: token));

        public async Task AssertWireAsync<T>(string wire, T expected, bool offloaded, CancellationToken token)
        {
            var serialized = Serializer.Serialize(expected);
            Assert.Equal(offloaded, PayloadReference.TryParse(wire, out var reference));
            if (!offloaded)
            {
                Assert.Equal(serialized, wire);
                return;
            }

            Assert.NotNull(reference);
            var bytes = Encoding.UTF8.GetBytes(serialized);
            Assert.Equal(bytes.LongLength, reference.Size);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), reference.ContentHash);
            Assert.Equal(bytes, await Services.GetRequiredService<DaprBinaryStoreClient>()
                .GetBinaryFileBytesAsync(reference.StoreName, reference.Key, token));
        }

        private async Task StopWorkerAsync()
        {
            if (_worker is null)
                return;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _worker.StopAsync(timeout.Token);
            }
            finally
            {
                await _worker.DisposeAsync();
                _worker = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopWorkerAsync();
            }
            finally
            {
                try
                {
                    await harness.DisposeAsync();
                }
                finally
                {
                    await environment.DisposeAsync();
                }
            }
        }
    }

    internal sealed class MultipleDocumentWorkflow : Workflow<PayloadReference<Document>[], Document[]>
    {
        public override async Task<Document[]> RunAsync(WorkflowContext context, PayloadReference<Document>[] input)
        {
            var references = await Task.WhenAll(input.Select(reference =>
                context.CallActivityAsync<PayloadReference<Document>>(nameof(EchoActivity), reference)));
            context.SetCustomStatus("waiting");
            await context.WaitForExternalEventAsync<bool>("resume");
            return await Task.WhenAll(references.Select(reference =>
                context.CallActivityAsync<Document>(nameof(EchoActivity), reference)));
        }
    }

    internal sealed class EventReceiverWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            context.SetCustomStatus("waiting");
            var inline = await context.WaitForExternalEventAsync<Document>("inline");
            var reference = await context.WaitForExternalEventAsync<Document>("reference");
            Assert.Equal(inline, reference);
            context.SetCustomStatus("received");
            await context.WaitForExternalEventAsync<bool>("finish");
            return $"{inline.Name}|{reference.Name}";
        }
    }

    internal sealed class MigrationWorkflow : Workflow<Document, Document>
    {
        public override async Task<Document> RunAsync(WorkflowContext context, Document input)
        {
            var value = input;
            for (var stage = 1; stage <= 2; stage++)
            {
                value = await context.CallActivityAsync<Document>(nameof(EchoActivity), value);
                context.SetCustomStatus($"waiting-{stage}");
                var raised = await context.WaitForExternalEventAsync<Document>($"resume-{stage}");
                Assert.Equal(input.Content, raised.Content);
                value = value with { Name = value.Name + ":" + raised.Name };
            }

            return await context.CallActivityAsync<Document>(nameof(EchoActivity), value);
        }
    }

    [WorkflowVersion(CanonicalName = FamilyName, Version = "1")]
    internal sealed class PayloadMigrationV1 : Workflow<Document, Document>
    {
        public override async Task<Document> RunAsync(WorkflowContext context, Document input)
        {
            var result = await context.CallActivityAsync<Document>(nameof(EchoActivity), input);
            context.SetCustomStatus("waiting");
            await context.WaitForExternalEventAsync<bool>("resume");
            return result;
        }
    }

    [WorkflowVersion(CanonicalName = FamilyName, Version = "2")]
    internal sealed class PayloadMigrationV2 : Workflow<PayloadReference<Document>, long>
    {
        public override async Task<long> RunAsync(WorkflowContext context, PayloadReference<Document> input)
        {
            var result = await context.CallActivityAsync<long>(nameof(StreamActivity), input);
            context.SetCustomStatus("waiting");
            await context.WaitForExternalEventAsync<bool>("resume");
            return result;
        }
    }

    private sealed class FirstVersionSelector : IWorkflowVersionSelector
    {
        public WorkflowVersionIdentity SelectLatest(string canonicalName,
            IReadOnlyCollection<WorkflowVersionIdentity> candidates, IWorkflowVersionStrategy strategy) =>
            candidates.OrderBy(candidate => candidate.Version, strategy).First();
    }

    internal sealed class OriginalWorkflow : Workflow<Document, long>
    {
        public override async Task<long> RunAsync(WorkflowContext context, Document input)
        {
            var result = await context.CallActivityAsync<long>(nameof(MeasureActivity), input);
            context.SetCustomStatus("waiting");
            await context.WaitForExternalEventAsync<bool>("resume");
            return result;
        }
    }

    internal sealed class PatchedWorkflow : Workflow<Document, long>
    {
        public override async Task<long> RunAsync(WorkflowContext context, Document input)
        {
            long result;
            if (context.IsPatched(ReferencePatch))
            {
                var reference =
                    await context.CallActivityAsync<PayloadReference<Document>>(nameof(StoreActivity), input);
                result = await context.CallActivityAsync<long>(nameof(StreamActivity), reference);
            }
            else
            {
                result = await context.CallActivityAsync<long>(nameof(MeasureActivity), input);
            }

            context.SetCustomStatus("waiting");
            await context.WaitForExternalEventAsync<bool>("resume");
            return result;
        }
    }

    internal sealed class IncompatibleInputWorkflow(ActivityCalls calls) : Workflow<PayloadReference<Document>, long>
    {
        public override Task<long> RunAsync(WorkflowContext context, PayloadReference<Document> input)
        {
            calls.Record(context.InstanceId, "patch-body");
            return Task.FromResult(context.IsPatched(ReferencePatch) ? input.Size : 0L);
        }
    }

    private sealed class EchoActivity(ActivityCalls calls) : WorkflowActivity<Document, Document>
    {
        public override Task<Document> RunAsync(WorkflowActivityContext context, Document input)
        {
            calls.Record(context.InstanceId, nameof(EchoActivity));
            return Task.FromResult(input with { Name = input.Name + ":echo" });
        }
    }

    private sealed class MeasureActivity(ActivityCalls calls, IDaprSerializer serializer)
        : WorkflowActivity<Document, long>
    {
        public override Task<long> RunAsync(WorkflowActivityContext context, Document input)
        {
            calls.Record(context.InstanceId, nameof(MeasureActivity));
            return Task.FromResult((long)Encoding.UTF8.GetByteCount(serializer.Serialize(input)));
        }
    }

    private sealed class StoreActivity(ActivityCalls calls, IWorkflowPayloadResolver resolver)
        : WorkflowActivity<Document, PayloadReference<Document>>
    {
        public override async Task<PayloadReference<Document>> RunAsync(WorkflowActivityContext context, Document input)
        {
            calls.Record(context.InstanceId, nameof(StoreActivity));
            return (await resolver.StoreAsync(input, context.InstanceId)).AsTyped<Document>();
        }
    }

    private sealed class StreamActivity(ActivityCalls calls, IWorkflowPayloadResolver resolver)
        : WorkflowActivity<PayloadReference<Document>, long>
    {
        public override async Task<long> RunAsync(WorkflowActivityContext context, PayloadReference<Document> input)
        {
            calls.Record(context.InstanceId, nameof(StreamActivity));
            await using var stream = await resolver.OpenAsync(input);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[16 * 1024];
            long count = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer)) != 0)
            {
                count += read;
                hash.AppendData(buffer, 0, read);
            }

            Assert.Equal(input.Size, count);
            Assert.Equal(input.ContentHash, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
            return count;
        }
    }
}
