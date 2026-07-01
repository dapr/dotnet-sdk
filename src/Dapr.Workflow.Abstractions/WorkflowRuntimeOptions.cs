// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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
using Dapr.Workflow.Worker;
using Grpc.Net.Client;

namespace Dapr.Workflow;

/// <summary>
/// Defines runtime options for workflows.
/// </summary>
public sealed class WorkflowRuntimeOptions
{
    private readonly List<Action<IWorkflowsFactory>> _registrationActions = [];
    private int _maxConcurrentWorkflows = 100;
    private int _maxConcurrentActivities = 100;

    /// <summary>
    /// Gets or sets the gRPC channel options used for connecting to the Dapr sidecar.
    /// </summary>
    public GrpcChannelOptions? GrpcChannelOptions { get; private set; }

    /// <summary>
    /// Gets or sets a value disabling the stateful-history optimization, in which the worker caches each
    /// instance's committed history per work-item stream so the sidecar can send only the new events (the
    /// delta) each turn instead of the full history. Disabled workers always receive the full history.
    /// Defaults to <c>false</c> (the optimization is on). A cache miss is always recovered safely via the
    /// GetInstanceHistory RPC, so this only affects per-turn bandwidth, never correctness.
    /// </summary>
    public bool DisableStatefulHistory { get; set; }

    /// <summary>
    /// Gets or sets the sliding time-to-live for cached instance histories. An instance's entry is reclaimed
    /// when it has gone idle (no turn) for longer than this. <c>null</c> (the default) uses the built-in
    /// default of one hour. Ignored when <see cref="DisableStatefulHistory"/> is <c>true</c>.
    /// </summary>
    public TimeSpan? HistoryCacheTtl { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of per-instance histories retained on a single work-item stream;
    /// least-recently-used entries are evicted beyond it. <c>0</c> (the default) uses the built-in default.
    /// Ignored when <see cref="DisableStatefulHistory"/> is <c>true</c>.
    /// </summary>
    public int HistoryCacheMaxInstances { get; set; }

    /// <summary>
    /// Gets or sets the byte budget for cached histories on a single work-item stream; least-recently-used
    /// entries are evicted beyond it. <c>0</c> (the default) means unlimited (bounded only by
    /// <see cref="HistoryCacheMaxInstances"/> and <see cref="HistoryCacheTtl"/>). Ignored when
    /// <see cref="DisableStatefulHistory"/> is <c>true</c>.
    /// </summary>
    public long HistoryCacheMaxBytes { get; set; }

    /// <summary>
    /// Gets the maximum number of concurrent workflow instances that can be executed at the same time.
    /// </summary>
    [Obsolete("This property is obsolete and no longer does anything - please use the options on the Dapr runtime instead. This property will be removed in a future SDK release.")]
    public int MaxConcurrentWorkflows
    {
        get => _maxConcurrentWorkflows;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxConcurrentWorkflows = value;
        }
    }

    /// <summary>
    /// Gets the maximum number of concurrent activities that can be executed at the same time.
    /// </summary>
    [Obsolete("This property is obsolete and no longer does anything - please use the options on the Dapr runtime instead. This property will be removed in a future SDK release.")]
    public int MaxConcurrentActivities
    {
        get => _maxConcurrentActivities;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxConcurrentActivities = value;
        }
    }

    /// <summary>
    /// Uses the provided <paramref name="grpcChannelOptions" /> for creating the <see cref="GrpcChannel" />.
    /// </summary>
    /// <param name="grpcChannelOptions">
    /// The <see cref="GrpcChannelOptions" /> to use for creating the <see cref="GrpcChannel" />.
    /// </param>
    public void UseGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions)
    {
        ArgumentNullException.ThrowIfNull(grpcChannelOptions);
        GrpcChannelOptions = grpcChannelOptions;
    }

    /// <summary>
    /// Registers a workflow as a function that takes a specified input type and returns a specified output type.
    /// </summary>
    public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput, Task<TOutput>> implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(implementation);
        _registrationActions.Add(factory => factory.RegisterWorkflow(name, implementation));
    }

    /// <summary>
    /// Registers a workflow class that derives from <see cref="Workflow{TInput, TOutput}"/>.
    /// </summary>
    public void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow
    {
        _registrationActions.Add(factory => factory.RegisterWorkflow<TWorkflow>(name));
    }

    /// <summary>
    /// Registers a workflow activity as a function that takes a specified input type and returns a specified output type.
    /// </summary>
    public void RegisterActivity<TInput, TOutput>(string name, Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(implementation);
        _registrationActions.Add(factory => factory.RegisterActivity(name, implementation));
    }

    /// <summary>
    /// Registers a workflow activity class that derives from <see cref="WorkflowActivity{TInput, TOutput}"/>.
    /// </summary>
    public void RegisterActivity<TActivity>(string? name = null) where TActivity : class, IWorkflowActivity
    {
        _registrationActions.Add(factory => factory.RegisterActivity<TActivity>(name));
    }

    /// <summary>
    /// Applies all registrations to the provided factory.
    /// </summary>
    public void ApplyRegistrations(IWorkflowsFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        foreach (var action in _registrationActions)
        {
            action(factory);
        }
    }
}
