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
            Assert.True(builder.GrpcSerializer.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void DaprClientBuilder_UsesPropertyNameCaseHandlingAsSpecified()
        {
            var serializer = new GrpcSerializer(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            });

            DaprClientBuilder builder = new DaprClientBuilder(serializer);
            Assert.False(builder.GrpcSerializer.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void DaprClientBuilder_UsesSpecifiedEndpoint()
        {
            const string endpoint = "https://dapr.io";

            DaprClientBuilder builder = new DaprClientBuilder();
            builder.UseEndpoint(endpoint);

            Assert.Equal(builder.DaprEndpoint, endpoint);
        }

        [Fact]
        public void DaprClientBuilder_UsesSpecifiedGrpcChannelOptions()
        {
            var grpcChannelOptions = new GrpcChannelOptions();

            DaprClientBuilder builder = new DaprClientBuilder();
            builder.UseGrpcChannelOptions(grpcChannelOptions);

            Assert.Equal(builder.GrpcChannelOptions, grpcChannelOptions);
        }
    }
}
