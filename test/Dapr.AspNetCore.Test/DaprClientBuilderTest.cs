using System.Text.Json;
using Dapr.Client;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class DaprClientBuilderTest
    {
        [Fact]
        public void DaprClientBuilder_UsesPropertyNameCaseHandlingInsensitiveByDefault()
        {
            DaprClientBuilder builder = new DaprClientBuilder();
            Assert.True(builder.jsonSerializerOptions.PropertyNameCaseInsensitive);
        }

        [Fact]
        public void DaprClientBuilder_UsesPropertyNameCaseHandlingAsSpecified()
        {
            DaprClientBuilder builder = new DaprClientBuilder();
            builder.UseJsonSerializationOptions(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            });
            Assert.False(builder.jsonSerializerOptions.PropertyNameCaseInsensitive);
        }
    }
}
