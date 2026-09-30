// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Common;

/// <summary>
/// Exposes information about which gRPC methods/types the connected Dapr runtime supports, using the
/// standard gRPC Server Reflection protocol. This is intended to enable backwards compatibility fallbacks
/// in the various Dapr SDKs.
/// </summary>
internal interface IDaprRuntimeCapabilities
{
    /// <summary>
    /// Gets whether the connected Dapr runtime supports the specified fully-qualified gRPC method.
    /// </summary>
    /// <param name="fullyQualifiedMethodName">The name of the fully-qualified gRPC method.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The runtime support state for the method.</returns>
    Task<DaprRuntimeSupport> GetMethodSupportAsync(string fullyQualifiedMethodName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the connected Dapr runtime supports the specified service.
    /// </summary>
    /// <param name="serviceName">The name of the service to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The runtime support state for the service.</returns>
    Task<DaprRuntimeSupport> GetServiceSupportAsync(string serviceName, CancellationToken cancellationToken = default);
}
