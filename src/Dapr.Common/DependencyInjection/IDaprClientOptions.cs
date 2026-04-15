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

using Dapr.Common.Serialization;

namespace Dapr.Common.DependencyInjection;

/// <summary>
/// Base options interface for all Dapr client configurations.
/// </summary>
/// <remarks>
/// Provides the common configuration properties shared by all Dapr clients
/// (Actors, Workflows, Pub/Sub, etc.). Specific clients extend this with
/// their own options. Used with <c>Microsoft.Extensions.Options</c> for
/// DI-based configuration.
/// </remarks>
public interface IDaprClientOptions
{
    /// <summary>
    /// Gets or sets the gRPC endpoint used to communicate with the Dapr sidecar.
    /// </summary>
    /// <remarks>
    /// Defaults to the value of the <c>DAPR_GRPC_ENDPOINT</c> environment variable,
    /// or <c>http://localhost:50001</c> if not set.
    /// </remarks>
    string? GrpcEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the HTTP endpoint used to communicate with the Dapr sidecar.
    /// </summary>
    /// <remarks>
    /// Defaults to the value of the <c>DAPR_HTTP_ENDPOINT</c> environment variable,
    /// or <c>http://localhost:3500</c> if not set.
    /// </remarks>
    string? HttpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the Dapr API token used for authentication with the sidecar.
    /// </summary>
    /// <remarks>
    /// Defaults to the value of the <c>DAPR_API_TOKEN</c> environment variable.
    /// </remarks>
    string? DaprApiToken { get; set; }
}
