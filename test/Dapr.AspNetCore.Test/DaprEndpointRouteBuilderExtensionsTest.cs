using System;
using System.Collections.Generic;
using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class DaprEndpointRouteBuilderExtensionsTest
    {
        [Fact]
        public void AddDapr_UsesSpecifiedDaprClientBuilderConfig()
        {
            var services = new ServiceCollection();
            var appBuilder = new ApplicationBuilder(services.BuildServiceProvider());
            var builder = new DefaultEndpointRouteBuilder(appBuilder);

            var clientBuilder = new Action<DaprClientBuilder>(
                builder => builder.UseJsonSerializationOptions(
                    new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = false
                    }
                )
            );

            builder.AddDapr(services, clientBuilder);

            var serviceProvider = services.BuildServiceProvider();
            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.False(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void AddDapr_UsesDefaultDaprClientBuilderConfig()
        {
            var services = new ServiceCollection();
            var appBuilder = new ApplicationBuilder(services.BuildServiceProvider());
            var builder = new DefaultEndpointRouteBuilder(appBuilder);

            builder.AddDapr(services);

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.True(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void AddDapr_RegistersDaprOnlyOnce()
        {
            var services = new ServiceCollection();
            var appBuilder = new ApplicationBuilder(services.BuildServiceProvider());
            var builder = new DefaultEndpointRouteBuilder(appBuilder);

            var clientBuilder = new Action<DaprClientBuilder>(
                builder => builder.UseJsonSerializationOptions(
                    new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = false
                    }
                )
            );

            // register with JsonSerializerOptions.PropertyNameCaseInsensitive = false
            builder.AddDapr(services, clientBuilder);

            // register with JsonSerializerOptions.PropertyNameCaseInsensitive = true (default)
            builder.AddDapr(services);

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.False(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        private class DefaultEndpointRouteBuilder : IEndpointRouteBuilder
        {
            public DefaultEndpointRouteBuilder(IApplicationBuilder applicationBuilder)
            {
                ApplicationBuilder = applicationBuilder ?? throw new ArgumentNullException(nameof(applicationBuilder));
                DataSources = new List<EndpointDataSource>();
            }

            public IApplicationBuilder ApplicationBuilder { get; }

            public IApplicationBuilder CreateApplicationBuilder() => ApplicationBuilder.New();

            public ICollection<EndpointDataSource> DataSources { get; }

            public IServiceProvider ServiceProvider => ApplicationBuilder.ApplicationServices;
        }
    }
}
