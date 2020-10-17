using System;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class DaprMvcBuilderExtensionsTest
    {
        [Fact]
        public void AddDapr_UsesSpecifiedDaprClientBuilderConfig()
        {
            var services = new ServiceCollection();
            const string endpoint = "https://dapr.io";

            var clientBuilder = new Action<DaprClientBuilder>(
                builder => builder.UseEndpoint(endpoint)
            );

            services.AddControllers().AddDapr(clientBuilder);

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.Equal(daprClient.Channel.Target, new Uri(endpoint).Authority);
        }

        [Fact]
        public void AddDapr_UsesDefaultDaprClientBuilderConfig()
        {
            var services = new ServiceCollection();

            services.AddControllers().AddDapr();

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.True(daprClient.Serializer.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void AddDapr_RegistersDaprOnlyOnce()
        {
            var services = new ServiceCollection();
            const string endpoint = "https://dapr.io";

            var clientBuilder = new Action<DaprClientBuilder>(
                builder => builder.UseEndpoint(endpoint)
            );

            // register with endpoint https://dapr.io
            services.AddControllers().AddDapr(clientBuilder);

            // register with endpoint http://127.0.0.1
            services.AddControllers().AddDapr();

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.Equal(daprClient.Channel.Target, new Uri(endpoint).Authority);
        }
    }
}
