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

using System.ComponentModel;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Dapr.Messaging.Subscribe.AppCallback;
using Dapr.Messaging.Subscribe.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dapr.Messaging;

/// <summary>
/// Registration entry point consumed by the source-generated <c>AddDaprMessaging</c> extension emitted
/// by <c>Dapr.Messaging.Generators</c>.
/// </summary>
/// <remarks>
/// <para>
/// This helper lives in the runtime assembly (which carries the ASP.NET Core framework reference and the
/// gRPC server packages) so the generated code emitted into a consumer's assembly does not have to
/// reference <c>Grpc.AspNetCore</c> or <c>Microsoft.AspNetCore.App</c> directly. The type is public only
/// so generated code can call it; it is not intended to be used directly from application code, and
/// doing so is reported by analyzer rule <c>DAPR1614</c>.
/// </para>
/// <para>
/// A single atomic <see cref="Register"/> method is exposed deliberately. Splitting registration across
/// several independently callable methods would make a partially registered messaging stack
/// representable; keeping it atomic means the only choice available to generated code is which optional
/// hosting features to opt into, expressed as <see cref="DaprMessagingFeatures"/>.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DaprMessagingRegistration
{
    /// <summary>
    /// Registers the complete Dapr messaging stack: options, the publishing client, the subscriber
    /// registry seam, and the hosting services required by <paramref name="features"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="DaprMessagingOptions"/>.</param>
    /// <param name="features">The optional hosting features required by the discovered subscriptions.</param>
    /// <returns>A builder allowing further Dapr messaging registration.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IDaprMessagingBuilder Register(
        IServiceCollection services,
        Action<DaprMessagingOptions>? configure,
        DaprMessagingFeatures features)
    {
        ArgumentNullException.ThrowIfNull(services);

        AddPublisher(services, configure);

        // Registered unconditionally: streaming subscriptions require no ASP.NET Core/gRPC server
        // hosting or routing, only a background client-initiated gRPC stream. When the generated
        // registry contains no Streaming descriptors, the hosted service is a no-op at startup.
        services.AddHostedService<StreamingSubscriberHostedService>();

        if (features.HasFlag(DaprMessagingFeatures.ProgrammaticSubscriptions))
        {
            // The sidecar pushes events to the application's AppCallback gRPC service, so ASP.NET Core
            // gRPC server hosting and the callback service itself are required.
            services.AddGrpc();
            services.TryAddTransient<DaprAppCallbackService>();
        }

        if (features.HasFlag(DaprMessagingFeatures.HttpSubscriptions))
        {
            // The /dapr/subscribe discovery endpoint and the delivery routes require routing support.
            services.AddRouting();
        }

        return new DaprMessagingBuilder(services);
    }

    private static void AddPublisher(IServiceCollection services, Action<DaprMessagingOptions>? configure)
    {
        services.AddOptions<DaprMessagingOptions>().Configure(options => configure?.Invoke(options));

        services.AddDaprPubSubClient(configure: (sp, b) =>
        {
            var opts = sp.GetRequiredService<IOptions<DaprMessagingOptions>>().Value;
            if (!string.IsNullOrEmpty(opts.DaprApiToken))
            {
                b.UseDaprApiToken(opts.DaprApiToken!);
            }

            b.UseJsonSerializationOptions(opts.JsonSerializerOptions);

            if (!string.IsNullOrEmpty(opts.DaprGrpcEndpoint))
            {
                b.UseGrpcEndpoint(opts.DaprGrpcEndpoint);
            }
        });

        services.TryAddSingleton<IDaprPublishSubscribeClient>(sp => sp.GetRequiredService<DaprPublishSubscribeClient>());
    }

    private sealed class DaprMessagingBuilder(IServiceCollection services) : IDaprMessagingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
