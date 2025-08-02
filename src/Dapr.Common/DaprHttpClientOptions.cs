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

using System.Text.Json;
using Grpc.Net.Client;

namespace Dapr.Common;

/// <summary>
/// Provides for a collection of options used to configure the Dapr <see cref="HttpClient"/> instance(s).
/// </summary>
public class DaprHttpClientOptions
{
    /// <summary>
    /// Gets or sets the HTTP endpoint used by the Dapr client.
    /// </summary>
    public string? HttpEndpoint { get; set; }
    
    /// <summary>
    /// Gets or sets the gRPC endpoint used by the Dapr client.
    /// </summary>
    public string? GrpcEndpoint { get; set; }
    
    /// <summary>
    /// Gets or sets the JSON serialization options.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }
    
    /// <summary>
    /// Gets or sets the gRPC channel options.
    /// </summary>
    public GrpcChannelOptions? GrpcChannelOptions { get; set; } = new GrpcChannelOptions
    {
        ThrowOperationCanceledOnCancellation = true
    };
    
    /// <summary>
    /// Gets or sets the API token used for Dapr authentication.
    /// </summary>
    public string? DaprApiToken { get; set; }
    
    /// <summary>
    /// Gets or sets the timeout for HTTP requests.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.Zero;
}
