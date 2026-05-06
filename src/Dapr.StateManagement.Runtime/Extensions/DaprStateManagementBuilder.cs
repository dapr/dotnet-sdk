// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Microsoft.Extensions.DependencyInjection;

namespace Dapr.StateManagement.Extensions;

/// <summary>
/// Used by the fluent registration API to configure a Dapr State Management client.
/// </summary>
/// <param name="services">The service collection to which the client is being registered.</param>
public sealed class DaprStateManagementBuilder(IServiceCollection services) : IDaprStateManagementBuilder
{
    /// <inheritdoc />
    public IServiceCollection Services { get; } = services;
}
