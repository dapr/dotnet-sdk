// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
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

namespace Dapr.SecretsManagement.Extensions;

/// <summary>
/// Used by the fluent registration builder to configure a Dapr Secrets Management client.
/// </summary>
/// <param name="services">The service collection to register services with.</param>
public sealed class DaprSecretsManagementBuilder(IServiceCollection services) : IDaprSecretsManagementBuilder
{
    /// <summary>
    /// Gets the registered services on the builder.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
