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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dapr.Messaging;

/// <summary>
/// Registration primitives consumed by the source-generated <c>AddDaprMessaging</c> extension emitted
/// by <c>Dapr.Messaging.Generators</c>.
/// </summary>
/// <remarks>
/// These helpers live in the runtime assembly (which carries the ASP.NET Core framework reference and
/// the gRPC server packages) so the generated code emitted into a consumer's assembly does not have to
/// reference <c>Grpc.AspNetCore</c> or <c>Microsoft.AspNetCore.App</c> directly. This type is public
/// only so generated code can call it; it is not intended to be used directly from application code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DaprMessagingRegistration
{
    /// <summary>
    /// Registers <see cref="DaprMessagingOptions"/> and the publisher surface
    /// (<see cref="DaprPublishSubscribeClient"/> and <see cref="IDaprPublishSubscribeClient"/>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="DaprMessagingOptions"/>.</param>
    /// <returns>The service collection.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddPublisher(
        IServiceCollection services,
        Action<DaprMessagingOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

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

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core gRPC server hosting and the Dapr <c>AppCallback</c> push service. Called by
    /// generated code only when at least one <see cref="DeliveryMode.Programmatic"/> subscription exists.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddProgrammaticSubscriptions(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGrpc();
        services.TryAddTransient<DaprAppCallbackService>();

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core routing support used by the HTTP subscription discovery and delivery
    /// endpoints. Called by generated code only when at least one <see cref="DeliveryMode.Http"/>
    /// subscription exists.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddHttpSubscriptions(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRouting();

        return services;
    }

    /// <summary>
    /// Creates the <see cref="IDaprMessagingBuilder"/> returned from the generated
    /// <c>AddDaprMessaging</c> extension.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The builder.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IDaprMessagingBuilder CreateBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new DaprMessagingBuilder(services);
    }

    private sealed class DaprMessagingBuilder(IServiceCollection services) : IDaprMessagingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
