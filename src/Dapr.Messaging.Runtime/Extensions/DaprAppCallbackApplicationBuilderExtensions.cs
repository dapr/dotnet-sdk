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

using Dapr.Messaging.Subscribe.AppCallback;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dapr.Messaging;

/// <summary>
/// ASP.NET Core endpoint-mapping extension that registers the Dapr <c>AppCallback</c> gRPC
/// service so the sidecar can push pub/sub events to the application.
/// </summary>
public static class DaprAppCallbackApplicationBuilderExtensions
{
    /// <summary>
    /// Maps the Dapr <c>AppCallback</c> gRPC service onto the endpoint routing pipeline. Call this
    /// alongside other <c>MapGrpcService</c> calls in <c>app.MapControllers</c>/<c>MapGrpcEndpoints</c>
    /// configuration for hosts that opt into <see cref="DeliveryMode.Programmatic"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint convention builder for the mapped service.</returns>
    public static IEndpointConventionBuilder MapDaprAppCallback(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGrpcService<DaprAppCallbackService>();
    }
}
