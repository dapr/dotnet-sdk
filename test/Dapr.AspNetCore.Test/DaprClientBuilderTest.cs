using System;
using System.Text.Json;
using Dapr.Client;
using Grpc.Net.Client;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class DaprClientBuilderTest
    {
        [Fact]
        public void DaprClientBuilder_UsesPropertyNameCaseHandlingInsensitiveByDefault()
        {
            DaprClientBuilder builder = new DaprClientBuilder();
            Assert.True(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void DaprClientBuilder_UsesPropertyNameCaseHandlingAsSpecified()
        {
            var builder = new DaprClientBuilder();
            builder.UseJsonSerializationOptions(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            });
            Assert.False(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void DaprClientBuilder_UsesThrowOperationCanceledOnCancellation_ByDefault()
        {
            var builder = new DaprClientBuilder();
            var daprClient = builder.Build();
            Assert.True(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
        }

        [Fact]
        public void DaprClientBuilder_DoesNotOverrideUserGrpcChannelOptions()
        {
            var builder = new DaprClientBuilder();
            var daprClient = builder.UseGrpcChannelOptions(new GrpcChannelOptions()).Build();
            Assert.False(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
        }

        [Fact]
        public void DaprClientBuilder_ValidatesGrpcEndpointScheme()
        {
            var builder = new DaprClientBuilder();
            builder.UseGrpcEndpoint("ftp://example.com");
            
            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.Equal("The gRPC endpoint must use http or https.", ex.Message);
        }

        [Fact]
        public void DaprClientBuilder_ValidatesHttpEndpointScheme()
        {
            var builder = new DaprClientBuilder();
            builder.UseHttpEndpoint("ftp://example.com");
            
            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.Equal("The HTTP endpoint must use http or https.", ex.Message);
        }

        [Fact]
        public void DaprClientBuilder_SetsApiToken()
        {
            var builder = new DaprClientBuilder();
            builder.UseDaprApiToken("test_token");
            builder.Build();
            Assert.Equal("test_token", builder.DaprApiToken);
        }

        [Fact]
        public void DaprClientBuilder_SetsNullApiToken()
        {
            var builder = new DaprClientBuilder();
            builder.UseDaprApiToken(null);
            builder.Build();
            Assert.Null(builder.DaprApiToken);
        }

        [Fact]
        public void DaprClientBuilder_ApiTokenSet_SetsApiTokenHeader()
        {
            var builder = new DaprClientBuilder();
            builder.UseDaprApiToken("test_token");
            var entry = DaprClient.GetDaprApiTokenHeader(builder.DaprApiToken);
            Assert.NotNull(entry);
            Assert.Equal("test_token", entry.Value.Value);
        }

        [Fact]
        public void DaprClientBuilder_ApiTokenNotSet_EmptyApiTokenHeader()
        {
            var builder = new DaprClientBuilder();
            var entry = DaprClient.GetDaprApiTokenHeader(builder.DaprApiToken);
            Assert.Equal(default, entry);
        }
    }
}
