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
            DaprClientBuilder builder = new DaprClientBuilder();
            builder.UseJsonSerializationOptions(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            });
            Assert.False(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void DaprClientBuilder_UsesThrowOperationCanceledOnCancellation_ByDefault()
        {
            DaprClientBuilder builder = new DaprClientBuilder();
            var daprClient = builder.Build();
            Assert.True(builder.GRPCChannelOptions.ThrowOperationCanceledOnCancellation);
        }

        [Fact]
        public void DaprClientBuilder_DoesNotOverrideUserGrpcChannelOptions()
        {
            var httpClient = new TestHttpClient();
            DaprClientBuilder builder = new DaprClientBuilder();
            var daprClient = builder.UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient }).Build();
            Assert.False(builder.GRPCChannelOptions.ThrowOperationCanceledOnCancellation);
        }
    }
}
