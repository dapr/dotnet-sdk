// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Jobs.Extensions;

/// <summary>
/// Used by the fluent registration builder to configure a Dapr Jobs client.
/// </summary>
/// <param name="services"></param>
public sealed class DaprJobsBuilder(IServiceCollection services) : IDaprJobsBuilder
{
    /// <summary>
    /// The registered services on the builder.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
