using System;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dapr.Common.Test;

public class DaprDefaultTest
{
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

            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
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

            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
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

            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
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

            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
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
    public void ShouldBuildHttpEndpointUsingOnlyPortConfiguration()
    {
        const string portVarName = "DAPR_HTTP_PORT";
        var original_HttpPort = Environment.GetEnvironmentVariable(portVarName);

        try
        {
            Environment.SetEnvironmentVariable(portVarName, "2569");

            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddEnvironmentVariables();
            var configuration = configurationBuilder.Build();

            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
            Assert.Equal("http://localhost:2569/", httpEndpoint);
        }
        finally
        {
            //Restore
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
            Environment.SetEnvironmentVariable(endpointVarName, "http://dapr.io");
            Environment.SetEnvironmentVariable(portVarName, "2569");

            var configurationBuilder = new ConfigurationBuilder();
            var configuration = configurationBuilder.Build();

            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
            Assert.Equal("http://dapr.io:80/", httpEndpoint);
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

            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
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

                var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
                Assert.Equal("http://localhost:3500/", httpEndpoint);
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

                var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
                Assert.Equal("http://localhost:7256/", httpEndpoint);
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

                var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint(configuration);
                Assert.Equal("http://localhost:3500/", httpEndpoint);
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

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", grpcEndpoint);
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
            const string endpointVarName = DaprDefaults.DaprGrpcEndpointName;
            const string portVarName = DaprDefaults.DaprGrpcPortName;
            var original_GrpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_GrpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, "https://grpc.dapr.io", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(portVarName, null, EnvironmentVariableTarget.Process); //Will use 443 from the endpoint value instead

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", grpcEndpoint);
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

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", grpcEndpoint);
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

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", grpcEndpoint);
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

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", grpcEndpoint);
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

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal("https://grpc.dapr.io:443/", grpcEndpoint);
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
            const string endpointVarName = DaprDefaults.DaprGrpcEndpointName;
            const string portVarName = DaprDefaults.DaprGrpcPortName;
            var original_grpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_grpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, null);
                Environment.SetEnvironmentVariable(portVarName, "7256");

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal($"{DaprDefaults.DefaultDaprScheme}://{DaprDefaults.DefaultDaprHost}:7256/", grpcEndpoint);
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
            const string endpointVarName = DaprDefaults.DaprGrpcEndpointName;
            const string portVarName = DaprDefaults.DaprGrpcPortName;
            var original_grpcEndpoint = Environment.GetEnvironmentVariable(endpointVarName);
            var original_grpcPort = Environment.GetEnvironmentVariable(portVarName);

            try
            {
                Environment.SetEnvironmentVariable(endpointVarName, null);
                Environment.SetEnvironmentVariable(portVarName, null);

                var configurationBuilder = new ConfigurationBuilder();
                var configuration = configurationBuilder.Build();

                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                Assert.Equal($"{DaprDefaults.DefaultDaprScheme}://{DaprDefaults.DefaultDaprHost}:{DaprDefaults.DefaultGrpcPort}/", grpcEndpoint);
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
            const string envVarName = DaprDefaults.DaprApiTokenName;
            var original_ApiToken = Environment.GetEnvironmentVariable(envVarName);

            try
            {
                const string apiToken = "abc123";
                Environment.SetEnvironmentVariable(envVarName, apiToken);

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var testApiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
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
            
            const string envVarName = $"test_{DaprDefaults.DaprApiTokenName}";
            var original_ApiToken = Environment.GetEnvironmentVariable(envVarName);

            try
            {
                const string prefix = "test_";

                const string apiToken = "abc123";
                Environment.SetEnvironmentVariable(envVarName, apiToken);

                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables(prefix);
                var configuration = configurationBuilder.Build();

                var testApiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
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
            const string envVarName = DaprDefaults.DaprApiTokenName;
            var original_ApiToken = Environment.GetEnvironmentVariable(envVarName);
            const string apiToken = "abc123";
            Environment.SetEnvironmentVariable(envVarName, apiToken);

            try
            {
                var configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddEnvironmentVariables();
                var configuration = configurationBuilder.Build();

                var testApiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
                Assert.Equal(apiToken, testApiToken);
            }
            finally
            {
                //Restore
                Environment.SetEnvironmentVariable(envVarName, original_ApiToken);
            }
        }
}
