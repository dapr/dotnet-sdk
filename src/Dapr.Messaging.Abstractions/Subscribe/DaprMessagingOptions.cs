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

using System.Text.Json;

namespace Dapr.Messaging;

/// <summary>
/// Options for the <c>Dapr.Messaging</c> stack, bound to <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>.
/// </summary>
public sealed class DaprMessagingOptions
{
    /// <summary>
    /// The Dapr API token forwarded on calls to the Dapr runtime.
    /// </summary>
    public string? DaprApiToken { get; set; }

    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> used for serializing publish payloads and deserializing
    /// subscriber messages when no source-generated <c>JsonSerializerContext</c> applies.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The gRPC endpoint of the Dapr runtime.
    /// </summary>
    public string DaprGrpcEndpoint { get; set; } = "http://localhost:50001";

    /// <summary>
    /// The delay applied between streaming reconnection attempts.
    /// </summary>
    public TimeSpan StreamingReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Obsolete alias for <see cref="StreamingReconnectDelay"/>.
    /// </summary>
    [Obsolete("Use StreamingReconnectDelay instead.")]
    public TimeSpan StreamingPullReconnectDelay
    {
        get => StreamingReconnectDelay;
        set => StreamingReconnectDelay = value;
    }
}
