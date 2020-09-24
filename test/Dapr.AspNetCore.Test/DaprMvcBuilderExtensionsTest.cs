using System;
using System.Text.Json;
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

            var clientBuilder = new Action<DaprClientBuilder>(
                builder => builder.UseJsonSerializationOptions(
                    new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = false
                    }
                )
            );

            services.AddControllers().AddDapr(clientBuilder);

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.False(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void AddDapr_UsesDefaultDaprClientBuilderConfig()
        {
            var services = new ServiceCollection();

            services.AddControllers().AddDapr();

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.True(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }
    }
}
