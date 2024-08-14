// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

#nullable enable

﻿using System;
using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class DaprServiceCollectionExtensionsTest : IDisposable
    {
        private string? _daprApiToken = null;
        private string? _daprAppToken = null;
        private string? _daprHttpEndpoint = null;
        private string? _daprHttpPort = null;
        private string? _daprGrpcEndpoint = null;
        private string? _daprGrpcPort = null;

        // setup
        public DaprServiceCollectionExtensionsTest()
        {
            _daprApiToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
            _daprAppToken = Environment.GetEnvironmentVariable("APP_API_TOKEN");
            _daprHttpEndpoint = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT");
            _daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
            _daprGrpcEndpoint = Environment.GetEnvironmentVariable("DAPR_GRPC_ENDPOINT");
            _daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
        }

        // teardown
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("DAPR_API_TOKEN", _daprApiToken);
            Environment.SetEnvironmentVariable("APP_API_TOKEN", _daprAppToken);
            Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", _daprHttpEndpoint);
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _daprHttpPort);
            Environment.SetEnvironmentVariable("DAPR_GRPC_ENDPOINT", _daprGrpcEndpoint);
            Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _daprGrpcPort);
        }

        [Fact]
        public void AddDaprClient_RegistersDaprClientOnlyOnce()
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

            // register with JsonSerializerOptions.PropertyNameCaseInsensitive = true (default)
            services.AddDaprClient();

            // register with PropertyNameCaseInsensitive = false
            services.AddDaprClient(clientBuilder);

            var serviceProvider = services.BuildServiceProvider();

            DaprClientGrpc? daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

            Assert.NotNull(daprClient);
            Assert.True(daprClient?.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void AddDaprClient_RegistersUsingDependencyFromIServiceProvider()
        {
            
            var services = new ServiceCollection();
            services.AddSingleton<TestConfigurationProvider>();
            services.AddDaprClient((provider, builder) =>
            {
                var configProvider = provider.GetRequiredService<TestConfigurationProvider>();
                var caseSensitivity = configProvider.GetCaseSensitivity();

                builder.UseJsonSerializationOptions(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = caseSensitivity
                });
            });

            var serviceProvider = services.BuildServiceProvider();
            
            DaprClientGrpc? client = serviceProvider.GetRequiredService<DaprClient>() as DaprClientGrpc;
            
            //Registers with case-insensitive as true by default, but we set as false above
            Assert.NotNull(client);
            Assert.False(client?.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void ShouldBuildHttpEndpointAndPortUsingPrefixedConfiguration()
        {
            const string endpointVarName = "test_DAPR_HTTP_ENDPOINT";
            const string portVarName = "test_DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                const string prefix = "test_";

                Environment.SetEnvironmentVariable(endpointVarName, "https://dapr.io");
                Environment.SetEnvironmentVariable(portVarName, null); //Will use 443 from the endpoint instead

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables(prefix);
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("https://dapr.io:443/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldBuildHttpEndpointAndPortUsingConfiguration()
        {
            const string endpointVarName = "DAPR_HTTP_ENDPOINT";
            const string portVarName = "DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://dapr.io");
                Environment.SetEnvironmentVariable(portVarName, null); //Will use 443 from the endpoint instead

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("https://dapr.io:443/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldBuildHttpEndpointUsingPrefixedConfiguration()
        {
            const string endpointVarName = "test_DAPR_HTTP_ENDPOINT";
            const string portVarName = "test_DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                const string prefix = "test_";

                Environment.SetEnvironmentVariable(endpointVarName, "https://dapr.io");
                Environment.SetEnvironmentVariable(portVarName, "2569");

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables(prefix);
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("https://dapr.io:2569/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldBuildHttpEndpointUsingConfiguration()
        {
            const string endpointVarName = "DAPR_HTTP_ENDPOINT";
            const string portVarName = "DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://dapr.io");
                Environment.SetEnvironmentVariable(portVarName, "2569");

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("https://dapr.io:2569/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldBuildHttpEndpointUsingEnvVarValues()
        {
            const string endpointVarName = "DAPR_HTTP_ENDPOINT";
            const string portVarName = "DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://dapr.io");
                Environment.SetEnvironmentVariable(portVarName, "2569");

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("https://dapr.io:2569/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldBuildHttpEndpointUsingMixedValues()
        {
            const string endpointVarName = "test_DAPR_HTTP_ENDPOINT";
            const string portVarName = "DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://dapr.io");
                Environment.SetEnvironmentVariable(portVarName, "2569");

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                configurationBuilder.AddEnvironmentVariables("test_");
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("https://dapr.io:2569/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldDefaultToEmptyHttpEndpoint()
        {
            const string endpointVarName = "DAPR_HTTP_ENDPOINT";
            const string portVarName = "DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, null);
                Environment.SetEnvironmentVariable(portVarName, null);

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal(string.Empty, httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldDefaultToLocalhostWithPort()
        {
            const string endpointVarName = "DAPR_HTTP_ENDPOINT";
            const string portVarName = "DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, null);
                Environment.SetEnvironmentVariable(portVarName, "7256");

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("http://127.0.0.1:7256/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldDefaultToLocalhostWithDefaultPort()
        {
            const string endpointVarName = "DAPR_HTTP_ENDPOINT";
            const string portVarName = "DAPR_HTTP_PORT";
            var original_HttpEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, null);
                Environment.SetEnvironmentVariable(portVarName, null);

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
                Assert.Equal("http://127.0.0.1:3500/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_HttpEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_HttpPort);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointAndPortUsingPrefixedConfiguration()
        {
            const string endpointVarName = "test_DAPR_GRPC_ENDPOINT";
            const string portVarName = "test_DAPR_GRPC_PORT";
            var original_GrpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_GrpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                const string prefix = "test_";

                Environment.SetEnvironmentVariable(endpointVarName, "https://grpc.dapr.io");
                Environment.SetEnvironmentVariable(portVarName, "2570");

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables(prefix);
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:2570/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_GrpcEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_GrpcPort);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointAndPortUsingConfiguration()
        {
            const string endpointVarName = "DAPR_GRPC_ENDPOINT";
            const string portVarName = "DAPR_GRPC_PORT";
            var original_GrpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_GrpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://grpc.dapr.io");
                Environment.SetEnvironmentVariable(portVarName, null); //Will use 443 from the endpoint value instead

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_GrpcEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_GrpcPort);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointUsingPrefixedConfiguration()
        {
            const string endpointVarName = "test_DAPR_GRPC_ENDPOINT";
            var original_GrpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);

            try
            {
                const string prefix = "test_";

                Environment.SetEnvironmentVariable(endpointVarName, "https://grpc.dapr.io");

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables(prefix);
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_GrpcEndpoint);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointUsingConfiguration()
        {
            const string endpointVarName = "DAPR_GRPC_ENDPOINT";
            const string portVarName = "DAPR_GRPC_PORT";
            var original_GrpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_GrpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://grpc.dapr.io");
                Environment.SetEnvironmentVariable(portVarName, null); //Will use 443 from the endpoint value instead

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_GrpcEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_GrpcPort);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointAndPortUsingEnvVarValues()
        {
            const string endpointVarName = "DAPR_GRPC_ENDPOINT";
            const string portVarName = "DAPR_GRPC_PORT";
            var original_GrpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_GrpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://grpc.dapr.io");
                Environment.SetEnvironmentVariable(portVarName, "4744"); //Will use 443 from the endpoint value instead

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:4744/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_GrpcEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_GrpcPort);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointUsingEnvVarValues()
        {
            const string endpointVarName = "DAPR_GRPC_ENDPOINT";
            const string portVarName = "DAPR_GRPC_PORT";
            var original_GrpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_GrpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://grpc.dapr.io");
                Environment.SetEnvironmentVariable(portVarName, null); //Will use 443 from the endpoint value instead

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_GrpcEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_GrpcPort);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointDefaultToLocalhostWithPort()
        {
            const string endpointVarName = "DAPR_GRPC_ENDPOINT";
            const string portVarName = "DAPR_GRPC_PORT";
            var original_grpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_grpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, null);
                Environment.SetEnvironmentVariable(portVarName, "7256");

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("http://127.0.0.1:7256/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_grpcEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_grpcPort);
            }
        }

        [Fact]
        public void ShouldBuildGrpcEndpointDefaultToLocalhostWithDefaultPort()
        {
            const string endpointVarName = "DAPR_GRPC_ENDPOINT";
            const string portVarName = "DAPR_GRPC_PORT";
            var original_grpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_grpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, null);
                Environment.SetEnvironmentVariable(portVarName, null);

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var httpEndpoint = DaprServiceCollectionExtensions.GetGrpcEndpoint(configuration);
                Assert.Equal("http://127.0.0.1:50001/", httpEndpoint);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(endpointVarName, original_grpcEndpoint);
                Environment.SetEnvironmentVariable(portVarName, original_grpcPort);
            }
        }

        [Fact]
        public void ShouldBuildApiTokenUsingConfiguration()
        {
            const string envVarName = "DAPR_API_TOKEN";
            var original_ApiToken = Environment.GetEnvironmentVariable(envVarName);

            try
            {
                const string apiToken = "abc123";
                Environment.SetEnvironmentVariable(envVarName, apiToken);

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var testApiToken = DaprServiceCollectionExtensions.GetApiToken(configuration);
                Assert.Equal(apiToken, testApiToken);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(envVarName, original_ApiToken);
            }
        }

        [Fact]
        public void ShouldBuildApiTokenUsingPrefixedConfiguration()
        {
            const string envVarName = "test_DAPR_API_TOKEN";
            var original_ApiToken = Environment.GetEnvironmentVariable(envVarName);

            try
            {
                const string prefix = "test_";

                const string apiToken = "abc123";
                Environment.SetEnvironmentVariable(envVarName, apiToken);

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables(prefix);
                var configuration = configurationBuilder.Build();

                var testApiToken = DaprServiceCollectionExtensions.GetApiToken(configuration);
                Assert.Equal(apiToken, testApiToken);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(envVarName, original_ApiToken);
            }
        }

        [Fact]
        public void ShouldBuildApiTokenWithEnvVarWhenConfigurationNotAvailable()
        {
            const string envVarName = "DAPR_API_TOKEN";
            var original_ApiToken = Environment.GetEnvironmentVariable(envVarName);
            const string apiToken = "abc123";
            Environment.SetEnvironmentVariable(envVarName, apiToken);

            try
            {
                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var testApiToken = DaprServiceCollectionExtensions.GetApiToken(configuration);
                Assert.Equal(apiToken, testApiToken);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(envVarName, original_ApiToken);
            }
        }

        [Fact]
        public void BuildEndpoint_WithOnlyEndpoint()
        {
            var output = DaprServiceCollectionExtensions.BuildEndpoint("https://dapr.io", null);
            Assert.Equal("https://dapr.io:443/", output);
        }

        [Fact]
        public void BuildEndpoint_WithEndpointAndPort()
        {
            var output = DaprServiceCollectionExtensions.BuildEndpoint("https://dapr.io", 3658);
            Assert.Equal("https://dapr.io:3658/", output);
        }

        [Fact]
        public void BuildEndpoint_WithOnlyPort()
        {
            var output = DaprServiceCollectionExtensions.BuildEndpoint(null, 3658);
            Assert.Equal("http://127.0.0.1:3658/", output);
        }

        //public void ShouldBuildHttpEndpointUsingConfiguration()
        //{
        //    var original_ApiToken = Environment.GetEnvironmentVariable("test_DAPR_API_TOKEN");
        //    var original_HttpEndpoint = Environment.GetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT");
        //    var original_HttpPort = Environment.GetEnvironmentVariable("test_DAPR_HTTP_PORT");
        //    var original_GrpcEndpoint = Environment.GetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT");
        //    var original_GrpcPort = Environment.GetEnvironmentVariable("test_DAPR_GRPC_PORT");

        //    try
        //    {
        //        const string prefix = "test_";

        //        Environment.SetEnvironmentVariable("test_DAPR_API_TOKEN", "abc123");
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT", "https://dapr.io");
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_PORT", "2569");
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT", "https://grpc.dapr.io");
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_PORT", "2570");

        //        var configurationBuilder = new ConfigurationBuilder();
        //        configurationBuilder.AddEnvironmentVariables(prefix);
        //        var configuration = configurationBuilder.Build();

        //        var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
        //        Assert.Equal("https://dapr.io:2569", httpEndpoint);
        //    }
        //    finally
        //    {

        //        //Restore
        //        Environment.SetEnvironmentVariable("test_DAPR_API_TOKEN", original_ApiToken);
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT", original_HttpEndpoint);
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_PORT", original_HttpPort);
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT", original_GrpcEndpoint);
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_PORT", original_GrpcPort);
        //    }
        //}

        //public void ShouldBuildHttpEndpointUsingConfiguration()
        //{
        //    var original_ApiToken = Environment.GetEnvironmentVariable("test_DAPR_API_TOKEN");
        //    var original_HttpEndpoint = Environment.GetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT");
        //    var original_HttpPort = Environment.GetEnvironmentVariable("test_DAPR_HTTP_PORT");
        //    var original_GrpcEndpoint = Environment.GetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT");
        //    var original_GrpcPort = Environment.GetEnvironmentVariable("test_DAPR_GRPC_PORT");

        //    try
        //    {
        //        const string prefix = "test_";

        //        Environment.SetEnvironmentVariable("test_DAPR_API_TOKEN", "abc123");
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT", "https://dapr.io");
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_PORT", "2569");
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT", "https://grpc.dapr.io");
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_PORT", "2570");

        //        var configurationBuilder = new ConfigurationBuilder();
        //        configurationBuilder.AddEnvironmentVariables(prefix);
        //        var configuration = configurationBuilder.Build();

        //        var httpEndpoint = DaprServiceCollectionExtensions.GetHttpEndpoint(configuration);
        //        Assert.Equal("https://dapr.io:2569", httpEndpoint);
        //    }
        //    finally
        //    {

        //        //Restore
        //        Environment.SetEnvironmentVariable("test_DAPR_API_TOKEN", original_ApiToken);
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT", original_HttpEndpoint);
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_PORT", original_HttpPort);
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT", original_GrpcEndpoint);
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_PORT", original_GrpcPort);
        //    }
        //}

        //public void PreferIConfigurationValuesDuringSetup()
        //{
        //    var original_ApiToken = Environment.GetEnvironmentVariable("test_DAPR_API_TOKEN");
        //    var original_HttpEndpoint = Environment.GetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT");
        //    var original_HttpPort = Environment.GetEnvironmentVariable("test_DAPR_HTTP_PORT");
        //    var original_GrpcEndpoint = Environment.GetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT");
        //    var original_GrpcPort = Environment.GetEnvironmentVariable("test_DAPR_GRPC_PORT");

        //    try
        //    {
        //        const string prefix = "test_";
            
        //        Environment.SetEnvironmentVariable("test_DAPR_API_TOKEN", "abc123");
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT", "https://dapr.io");
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_PORT", "2569");
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT", "https://grpc.dapr.io");
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_PORT", "2570");

        //        var builder = WebApplication.CreateBuilder(new string[] {});
        //        builder.Configuration.AddEnvironmentVariables(prefix);
        //        builder.Services.AddDaprClient();

        //    }
        //    finally
        //    {

        //        //Restore
        //        Environment.SetEnvironmentVariable("test_DAPR_API_TOKEN", original_ApiToken);
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_ENDPOINT", original_HttpEndpoint);
        //        Environment.SetEnvironmentVariable("test_DAPR_HTTP_PORT", original_HttpPort);
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_ENDPOINT", original_GrpcEndpoint);
        //        Environment.SetEnvironmentVariable("test_DAPR_GRPC_PORT", original_GrpcPort);
        //    }
        //}

        

#if NET8_0_OR_GREATER
        [Fact]
        public void AddDaprClient_WithKeyedServices()
        {
            var services = new ServiceCollection();

            services.AddKeyedSingleton("key1", new Object());

            services.AddDaprClient();

            var serviceProvider = services.BuildServiceProvider();

            var daprClient = serviceProvider.GetService<DaprClient>();

            Assert.NotNull(daprClient);
        }
#endif
        
        private class TestConfigurationProvider
        {
            public bool GetCaseSensitivity() => false;
        }
    }
}
