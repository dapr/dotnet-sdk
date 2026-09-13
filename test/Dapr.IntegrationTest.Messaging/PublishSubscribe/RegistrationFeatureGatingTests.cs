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
using Dapr.Messaging.Subscribe.AppCallback;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

/// <summary>
/// Verifies that the feature gating performed by the source generator actually gates. The generated
/// <c>AddDaprMessaging</c> for this assembly declares both programmatic and HTTP subscriptions, so both
/// sets of hosting services must be present; the flag-specific behaviour is verified by calling
/// <c>DaprMessagingRegistration.Register</c> with explicit feature combinations.
/// </summary>
/// <remarks>
/// Direct calls to <c>DaprMessagingRegistration</c> are reported by DAPR1614 in application code. They
/// are intentional here: this is the only way to assert what each individual feature flag contributes
/// without creating additional assemblies with different handler sets.
/// </remarks>
#pragma warning disable DAPR1614
public class RegistrationFeatureGatingTests
{
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static bool HasGrpcServerHosting(IServiceCollection services) =>
        services.Any(d => d.ServiceType.Namespace?.StartsWith("Grpc.AspNetCore", StringComparison.Ordinal) == true);

    private static bool HasRouting(IServiceCollection services) =>
        services.Any(d => d.ServiceType == typeof(EndpointDataSource));

    [Fact]
    public void Register_WithNoFeatures_RegistersPublisherButNoHostingServices()
    {
        var services = CreateServices();
        DaprMessagingRegistration.Register(services, null, DaprMessagingFeatures.None);

        // The publishing surface is unconditional.
        Assert.Contains(services, d => d.ServiceType == typeof(IDaprPublishSubscribeClient));

        // Streaming-only applications need neither gRPC server hosting nor routing.
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(DaprAppCallbackService));
        Assert.False(HasGrpcServerHosting(services));
        Assert.False(HasRouting(services));
    }

    [Fact]
    public void Register_WithProgrammaticOnly_RegistersGrpcHostingAndAppCallback()
    {
        var services = CreateServices();
        DaprMessagingRegistration.Register(services, null, DaprMessagingFeatures.ProgrammaticSubscriptions);

        Assert.Contains(services, d => d.ServiceType == typeof(DaprAppCallbackService));
        Assert.True(HasGrpcServerHosting(services));
    }

    [Fact]
    public void Register_WithHttpOnly_RegistersRoutingButNoGrpcHosting()
    {
        var services = CreateServices();
        DaprMessagingRegistration.Register(services, null, DaprMessagingFeatures.HttpSubscriptions);

        Assert.True(HasRouting(services));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(DaprAppCallbackService));
        Assert.False(HasGrpcServerHosting(services));
    }

    [Fact]
    public void Register_WithBothFeatures_RegistersAppCallbackAndRouting()
    {
        var services = CreateServices();
        DaprMessagingRegistration.Register(
            services,
            null,
            DaprMessagingFeatures.ProgrammaticSubscriptions | DaprMessagingFeatures.HttpSubscriptions);

        Assert.Contains(services, d => d.ServiceType == typeof(DaprAppCallbackService));
        Assert.True(HasGrpcServerHosting(services));
        Assert.True(HasRouting(services));
    }

    [Fact]
    public void Register_AppliesSuppliedOptions()
    {
        var services = CreateServices();
        DaprMessagingRegistration.Register(
            services,
            options => options.DaprApiToken = "token-from-options",
            DaprMessagingFeatures.None);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DaprMessagingOptions>>();

        Assert.Equal("token-from-options", options.Value.DaprApiToken);
    }

    /// <summary>
    /// This assembly declares both a programmatic and two HTTP handlers, so the generated single-call
    /// registration must opt into both feature sets without the developer asking for either.
    /// </summary>
    [Fact]
    public void GeneratedAddDaprMessaging_OptsIntoBothFeatureSetsForThisAssembly()
    {
        var services = CreateServices();
        services.AddSingleton<IntegrationOrderState>();
        services.AddSingleton<HttpNotificationState>();
        services.AddSingleton<HttpBulkState>();

        services.AddDaprMessaging();

        Assert.Contains(services, d => d.ServiceType == typeof(IDaprPublishSubscribeClient));
        Assert.Contains(services, d => d.ServiceType == typeof(IDaprMessagingSubscriberRegistry));
        Assert.Contains(services, d => d.ServiceType == typeof(DaprAppCallbackService));
        Assert.True(HasGrpcServerHosting(services));
        Assert.True(HasRouting(services));
    }

    /// <summary>
    /// The single-call API must be safe to call more than once, since library authors and application
    /// authors may both register the messaging stack.
    /// </summary>
    [Fact]
    public void GeneratedAddDaprMessaging_IsIdempotent()
    {
        var services = CreateServices();
        services.AddSingleton<IntegrationOrderState>();
        services.AddSingleton<HttpNotificationState>();
        services.AddSingleton<HttpBulkState>();

        services.AddDaprMessaging();
        services.AddDaprMessaging();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IDaprMessagingSubscriberRegistry>());
        Assert.NotNull(provider.GetRequiredService<DaprAppCallbackService>());
    }
}
#pragma warning restore DAPR1614
