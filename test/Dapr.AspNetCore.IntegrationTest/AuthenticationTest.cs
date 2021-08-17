using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dapr.AspNetCore.IntegrationTest.App;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Dapr.AspNetCore.IntegrationTest
{
    public class AuthenticationTest
    {
        [Fact]
        public async Task ValidToken_ShouldBeAuthenticatedAndAuthorized()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var userInfo = new UserInfo
                {
                    Name = "jimmy"
                };
                var httpClient = factory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/requires-api-token")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(userInfo), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Dapr-Api-Token", "abcdefg");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseUserInfo = JsonConvert.DeserializeObject<UserInfo>(responseContent);
                responseUserInfo.Name.Should().Be(userInfo.Name);
            }
        }

        [Fact]
        public async Task InvalidToken_ShouldBeUnauthorized()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var userInfo = new UserInfo
                {
                    Name = "jimmy"
                };
                var httpClient = factory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/requires-api-token")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(userInfo), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Dapr-Api-Token", "asdfgh");
                var response = await httpClient.SendAsync(request);
                
                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }
        }
    }
}
