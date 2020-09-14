using System;
using System.Reflection;
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

            var daprClient = serviceProvider.GetService<DaprClient>();

            var jsonSerializerOptions = daprClient
                .GetType()
                .GetField("jsonSerializerOptions", 
                    BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic)
                .GetValue(daprClient) as JsonSerializerOptions;

            Assert.False(jsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void AddDapr_UsesDefaultDaprClientBuilderConfig()
        {
            var services = new ServiceCollection();

            services.AddControllers().AddDapr();

            var serviceProvider = services.BuildServiceProvider();

            var daprClient = serviceProvider.GetService<DaprClient>();

            var jsonSerializerOptions = daprClient
                .GetType()
                .GetField("jsonSerializerOptions",
                    BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic)
                .GetValue(daprClient) as JsonSerializerOptions;

            Assert.True(jsonSerializerOptions.PropertyNameCaseInsensitive);
        }
    }
}
