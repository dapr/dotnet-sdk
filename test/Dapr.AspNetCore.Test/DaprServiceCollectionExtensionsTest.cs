using System;
using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class DaprServiceCollectionExtensionsTest
    {
        [Fact]
        public void AddDaprClient_RegistersDaprClientOnlyOnce()
        {
            var services = new ServiceCollection();
            const string endpoint = "https://dapr.io";

            var clientBuilder = new Action<DaprClientBuilder>(
                builder => builder.UseEndpoint(endpoint)
            );

            // register with endpoint https://dapr.io
            services.AddDaprClient(clientBuilder);

            // register with endpoint http://127.0.0.1
            services.AddDaprClient();

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.Equal(daprClient.Channel.Target, new Uri(endpoint).Authority);
        }

        [Fact]
        public void AddDaprClient_UsesRegisteredGrpcSerializer()
        {
            var serializer = new GrpcSerializer(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            });
            var services = new ServiceCollection();
            services.AddSingleton(_ => serializer);

            services.AddDaprClient();

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.Equal(daprClient.Serializer, serializer);
        }
    }
}
