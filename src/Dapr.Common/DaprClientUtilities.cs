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

using System.Net.Http.Headers;
using System.Reflection;
using Grpc.Core;

namespace Dapr.Common;

internal static class DaprClientUtilities
{
    /// <summary>
    /// Provisions the gRPC call options used to provision the various Dapr clients.
    /// </summary>
    /// <param name="daprApiToken">The Dapr API token, if any.</param>
    /// <param name="assembly">The assembly the user agent is built from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The gRPC call options.</returns>
    internal static CallOptions ConfigureGrpcCallOptions(Assembly assembly, string? daprApiToken, CancellationToken cancellationToken = default)
    {
        var callOptions = new CallOptions(headers: new Metadata(), cancellationToken: cancellationToken);
        
        //Add the user-agent header to the gRPC call options
        var assemblyVersion = assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;
        var userAgent = new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}").ToString();
        callOptions.Headers!.Add("User-Agent", userAgent);

        //Add the API token to the headers as well if it's populated
        if (daprApiToken is not null)
        {
            var apiTokenHeader = GetDaprApiTokenHeader(daprApiToken);
            if (apiTokenHeader is not null)
            {
                callOptions.Headers.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);
            }
        }

        return callOptions;
    }
    
    /// <summary>
    /// Used to create the user-agent from the assembly attributes.
    /// </summary>
    /// <param name="assembly">The assembly the client is being built for.</param>
    /// <returns>The header value containing the user agent information.</returns>
    public static ProductInfoHeaderValue GetUserAgent(Assembly assembly)
    {
        var assemblyVersion = assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;
        return new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}");
    }

    /// <summary>
    /// Used to provision the header used for the Dapr API token on the HTTP or gRPC connection.
    /// </summary>
    /// <param name="daprApiToken">The value of the Dapr API token.</param>
    /// <returns>If a Dapr API token exists, the key/value pair to use for the header; otherwise null.</returns>
    public static KeyValuePair<string, string>? GetDaprApiTokenHeader(string? daprApiToken) =>
        string.IsNullOrWhiteSpace(daprApiToken)
            ? null
            : new KeyValuePair<string, string>("dapr-api-token", daprApiToken);
}
