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

using System.Net.Http.Headers;
using System.Reflection;
using Grpc.Core;

namespace Dapr.Actors.Next.Core.Transport;

internal static class DaprActorGrpcCallOptions
{
    internal static CallOptions Create(string? daprApiToken, CancellationToken cancellationToken = default)
    {
        var headers = new Metadata();
        headers.Add("User-Agent", GetUserAgent());

        if (!string.IsNullOrWhiteSpace(daprApiToken))
        {
            headers.Add("dapr-api-token", daprApiToken);
        }

        return new CallOptions(headers: headers, cancellationToken: cancellationToken);
    }

    private static string GetUserAgent()
    {
        var assemblyVersion = typeof(DaprActorGrpcCallOptions).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;

        return new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}").ToString();
    }
}
