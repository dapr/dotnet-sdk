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

using Grpc.Net.Client;

namespace Dapr.Workflow;

using Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Worker;

/// <summary>
/// Defines runtime options for workflows.
/// </summary>
public sealed class WorkflowRuntimeOptions
{
    private readonly List<Action<WorkflowsFactory>> _registrationActions = [];
    private int _maxConcurrentWorkflows = 100;
    private int _maxConcurrentActivities = 100;
    
    /// <summary>
    /// Gets the maximum number of concurrent workflow instances that can be executed at the same time.
    /// </summary>
    /// <remarks>
    /// The default is 100. Setting this to a higher value can improve throughput but will also increase memory
    /// usage.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
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
    /// <remarks>
    /// The default value is 100. Setting this to a higher value can improve throughput, but will also increase
    /// memory usage.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
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
    /// Gets or sets the gRPC channel options used for connecting to the Dapr sidecar.
    /// </summary>
    internal GrpcChannelOptions? GrpcChannelOptions { get; private set; }

    /// <summary>
    /// Registers a workflow as a function that takes a specified input type and returns a specified output type.
    /// </summary>
    /// <typeparam name="TInput">The type of the workflow input.</typeparam>
    /// <typeparam name="TOutput">The type of the workflow output.</typeparam>
    /// <param name="name">Workflow name.</param>
    /// <param name="implementation">Function implementing the workflow definition.</param>
    public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput, Task<TOutput>> implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(implementation);
        
        // Store as a registration action to be applied to WorkflowsFactory later
        _registrationActions.Add(factory => factory.RegisterWorkflow(name, implementation));
    }

    /// <summary>
    /// Registers a workflow class that derives from <see cref="Workflow{TInput, TOutput}"/>.
    /// </summary>
    /// <typeparam name="TWorkflow">The <see cref="Workflow{TInput, TOutput}"/> type to register.</typeparam>
    /// <param name="name">
    /// Workflow name. If not specified, then the name of <typeparamref name="TWorkflow"/> is used.
    /// </param>
    public void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow
    {
        _registrationActions.Add(factory => factory.RegisterWorkflow<TWorkflow>(name));
    }
    
    /// <summary>
    /// Registers a workflow activity as a function that takes a specified input type and returns a specified output type.
    /// </summary>
    /// <typeparam name="TInput">The type of the activity input.</typeparam>
    /// <typeparam name="TOutput">The type of the activity output.</typeparam>
    /// <param name="name">Activity name.</param>
    /// <param name="implementation">Activity implementation.</param>
    public void RegisterActivity<TInput, TOutput>(string name, Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(implementation);
        
        _registrationActions.Add(factory => factory.RegisterActivity(name, implementation));
    }
    
    /// <summary>
    /// Registers a workflow activity class that derives from <see cref="WorkflowActivity{TInput, TOutput}"/>.
    /// </summary>
    /// <typeparam name="TActivity">The <see cref="WorkflowActivity{TInput, TOutput}"/> type to register.</typeparam>
    /// <param name="name">
    /// Activity name. If not specified, then the name of <typeparamref name="TActivity"/> is used.
    /// </param>
    public void RegisterActivity<TActivity>(string? name = null)
        where TActivity : class, IWorkflowActivity
    {
        _registrationActions.Add(factory => factory.RegisterActivity<TActivity>(name));
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
    /// Applies all registrations to the provided factory.
    /// </summary>
    /// <param name="factory">The factory to apply registrations to.</param>
    internal void ApplyRegistrations(WorkflowsFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        
        foreach (var action in _registrationActions)
        {
            action(factory);
        }
    }
}
