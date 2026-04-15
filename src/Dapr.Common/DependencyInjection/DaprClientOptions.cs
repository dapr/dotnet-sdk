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

namespace Dapr.Common.DependencyInjection;

/// <summary>
/// Default implementation of <see cref="IDaprClientOptions"/> providing base configuration
/// for all Dapr clients.
/// </summary>
/// <remarks>
/// This class can be used directly for simple scenarios or extended by specific Dapr
/// client options classes. Values default to environment variable resolution via
/// <see cref="DaprDefaults"/>.
/// </remarks>
public class DaprClientOptions : IDaprClientOptions
{
    /// <inheritdoc />
    public string? GrpcEndpoint { get; set; }

    /// <inheritdoc />
    public string? HttpEndpoint { get; set; }

    /// <inheritdoc />
    public string? DaprApiToken { get; set; }
}
