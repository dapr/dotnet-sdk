// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors.Client;

using System;
using System.Text.Json;
using Dapr.Common.Serialization;

/// <summary>
/// The class containing customizable options for how the Actor Proxy is initialized.
/// </summary>
public class ActorProxyOptions
{
    // TODO: Add actor retry settings

    private JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    /// <summary>
    /// The constructor
    /// </summary>
    public ActorProxyOptions()
    {
    }

    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> used for actor proxy message serialization in non-remoting invocation.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions
    {
        get => this.jsonSerializerOptions;
        set => this.jsonSerializerOptions = value ??
                                            throw new ArgumentNullException(nameof(JsonSerializerOptions), $"{nameof(ActorProxyOptions)}.{nameof(JsonSerializerOptions)} cannot be null");
    }

    /// <summary>
    /// The Dapr Api Token that is added to the header for all requests.
    /// </summary>
    public string DaprApiToken { get; set; } = DaprDefaults.GetDefaultDaprApiToken(null);

    /// <summary>
    /// Gets or sets the HTTP endpoint URI used to communicate with the Dapr sidecar.
    /// </summary>
    /// <remarks>
    /// The URI endpoint to use for HTTP calls to the Dapr runtime. The default value will be 
    /// <c>http://127.0.0.1:DAPR_HTTP_PORT</c> where <c>DAPR_HTTP_PORT</c> represents the value of the 
    /// <c>DAPR_HTTP_PORT</c> environment variable.
    /// </remarks>
    /// <value></value>
    public string HttpEndpoint { get; set; } = DaprDefaults.GetDefaultHttpEndpoint();

    /// <summary>
    /// The timeout allowed for an actor request. Can be set to System.Threading.Timeout.InfiniteTimeSpan to disable any timeouts.
    /// </summary>
    public TimeSpan? RequestTimeout { get; set; } = null;

    /// <summary>
    /// Enable JSON serialization for actor proxy message serialization in both remoting and non-remoting invocations.
    /// </summary>
    public bool UseJsonSerialization { get; set; }

    /// <summary>
    /// Gets or sets an optional <see cref="IDaprSerializer"/> to use for all actor proxy serialization.
    /// </summary>
    /// <remarks>
    /// When set, this serializer is used for both remoting and non-remoting invocations, taking precedence
    /// over <see cref="JsonSerializerOptions"/> and <see cref="UseJsonSerialization"/>.
    /// This provides a unified serialization experience consistent with other Dapr SDK components.
    /// </remarks>
    public IDaprSerializer DaprSerializer { get; set; }
}