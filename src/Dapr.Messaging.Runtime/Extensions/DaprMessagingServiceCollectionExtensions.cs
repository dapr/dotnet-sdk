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

using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration entry point for the <c>Dapr.Messaging</c> stack.
/// </summary>
public static class DaprMessagingServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr messaging services (publish + subscribe plumbing) to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="DaprMessagingOptions"/>.</param>
    /// <returns>A builder for further registration.</returns>
    public static IDaprMessagingBuilder AddDaprMessaging(
        this IServiceCollection services,
        Action<DaprMessagingOptions>? configure = null)
    {
        services.AddOptions<DaprMessagingOptions>().Configure(options => configure?.Invoke(options));
        return new DaprMessagingBuilder(services);
    }

    /// <summary>
    /// Adds the Dapr publish/subscribe client (publish + streaming-pull surface) to the service collection.
    /// </summary>
    public static IDaprMessagingBuilder AddDaprPubSub(this IDaprMessagingBuilder builder)
    {
        builder.Services.AddDaprPubSubClient(
            configure: (sp, b) =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DaprMessagingOptions>>().Value;
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
        builder.Services.TryAddSingleton<IDaprPublishSubscribeClient>(sp => sp.GetRequiredService<DaprPublishSubscribeClient>());
        return builder;
    }

    /// <summary>
    /// Marks the service collection as preparing for source-generated subscribers, registers
    /// ASP.NET Core gRPC server hosting (<c>AddGrpc()</c>), and registers the <c>AppCallback</c>
    /// gRPC push service in DI. The generated <c>AddGeneratedSubscribers</c> extension (emitted by
    /// the <c>Dapr.Messaging.Generators</c> source generator into the consumer's assembly)
    /// performs the actual dispatcher/registry registration.
    /// </summary>
    public static IDaprMessagingBuilder AddDaprSubscriber(this IDaprMessagingBuilder builder)
    {
        builder.Services.AddGrpc();
        builder.Services.TryAddTransient<global::Dapr.Messaging.Subscribe.AppCallback.DaprAppCallbackService>();
        return builder;
    }

    private sealed class DaprMessagingBuilder(IServiceCollection services) : global::Dapr.Messaging.IDaprMessagingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
