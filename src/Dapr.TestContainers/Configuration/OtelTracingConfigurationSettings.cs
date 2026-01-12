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
//  ------------------------------------------------------------------------

namespace Dapr.TestContainers.Configuration;

/// <summary>
/// Configuration settings for OTEL tracing.
/// </summary>
/// <param name="endpointAddress">The tracing endpoint address.</param>
/// <param name="isSecure">Indicates whether the endpoint is secure.</param>
/// <param name="protocol">The tracing protocol.</param>
public sealed class OtelTracingConfigurationSettings(string endpointAddress, bool isSecure, string protocol) : ConfigurationSettings
{
    /// <summary>
    /// The endpoint address.
    /// </summary>
	public string EndpointAddress => endpointAddress;
    /// <summary>
    /// Whether the endpoint address is secure.
    /// </summary>
	public bool IsSecure => isSecure;
    /// <summary>
    /// The collection protocol.
    /// </summary>
	public string Protocol => protocol;
}
