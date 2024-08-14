using Xunit;

namespace Dapr.Common.Test
{
    public class DaprGenericBuilderTests
    {
        [Fact]
        public void ShouldUseValuesFromTheEnvironmentVariables()
        {
            var builder = new MockDaprBuilder();

            var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint();
            var httpEndpoint = DaprDefaults.GetDefaultHttpEndpoint();
            var apiToken = DaprDefaults.GetDefaultDaprApiToken();

            Assert.Equal(grpcEndpoint, builder.GrpcEndpoint);
            Assert.Equal(httpEndpoint, builder.HttpEndpoint);
            Assert.Equal(apiToken, builder.DaprApiToken);
        }

        [Fact]
        public void ShouldUseConfiguredValuesAtRegistration()
        {
            const string httpEndpoint = "https://http.abc123.com";
            const string grpcEndpoint = "https://grpc.abc123.com";
            const string apiToken = "abc123";

            var builder = new MockDaprBuilder();
            builder.UseHttpEndpoint(httpEndpoint);
            builder.UseGrpcEndpoint(grpcEndpoint);
            builder.UseDaprApiToken(apiToken);

            Assert.Equal(httpEndpoint, builder.HttpEndpoint);
            Assert.Equal(grpcEndpoint, builder.GrpcEndpoint);
            Assert.Equal(apiToken, builder.DaprApiToken);
        }
    }
}
