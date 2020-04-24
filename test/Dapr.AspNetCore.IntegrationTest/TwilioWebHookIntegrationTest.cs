namespace Dapr.AspNetCore.IntegrationTest
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.AspNetCore.IntegrationTest.App;
    using FluentAssertions;
    using Xunit;

    public class TwilioWebHookIntegrationTest
    {
        private const string formURLEncodedContent = "FieldA=stringInfoA&FieldB=stringInfoB&FieldC=stringInfoC";
        private const string mediaTypeHeaderValue = "application/x-www-form-urlencoded";
        private readonly JsonSerializerOptions options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        [Fact]
        public async Task CanSendEmptyFormURLEncodedPost()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/formurlencoded")
                {
                    Content = new StringContent("", Encoding.UTF8)
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaTypeHeaderValue);

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
        }

        [Fact]
        public async Task CanSendVoiceRequestFromTwilio()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/formurlencoded")
                {
                    Content = new StringContent(formURLEncodedContent, Encoding.UTF8)
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaTypeHeaderValue);

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await JsonSerializer.DeserializeAsync<FormURLEncodingPayload>(await response.Content.ReadAsStreamAsync(), this.options);
                widget.FieldA.Should().Be("stringInfoA");
                widget.FieldB.Should().Be("stringInfoB");
                widget.FieldC.Should().Be("stringInfoC");
            }
        }
    }
}
