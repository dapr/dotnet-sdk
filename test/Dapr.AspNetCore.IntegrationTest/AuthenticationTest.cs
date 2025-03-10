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

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dapr.AspNetCore.IntegrationTest.App;
using Shouldly;
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
                var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/requires-api-token")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(userInfo), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Dapr-Api-Token", "abcdefg");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseUserInfo = JsonConvert.DeserializeObject<UserInfo>(responseContent);
                responseUserInfo.Name.ShouldBe(userInfo.Name);
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
                var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/requires-api-token")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(userInfo), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Dapr-Api-Token", "asdfgh");
                var response = await httpClient.SendAsync(request);

                response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }
        }
    }
}
