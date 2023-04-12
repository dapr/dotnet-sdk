using Dapr.Workflow;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WorkflowConsoleApp.Models;

namespace WorkflowConsoleApp.Activities
{

    public class AuthenticationActivity : WorkflowActivity<InventoryRequest, AuthenticationResult>
    {
        readonly ILogger logger;
        private static readonly HttpClient client = new HttpClient();

        private static HttpClient sharedClient = new()
        {
            BaseAddress = new Uri("http://localhost:5074/"),
        };
        public AuthenticationActivity(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<AuthenticationActivity>();
        }

        public override Task<AuthenticationResult> RunAsync(WorkflowActivityContext context, InventoryRequest req)
        {
            this.logger.LogInformation(
            "Authenticating request for order '{requestId}' of {quantity} {name}",
            req.RequestId,
            req.Quantity,
            req.ItemName);
            
            // Since company cars are expensive and exclusive, we want to authenticate before allowing the purchase
            if (req.ItemName != "companycar") {
                return Task.FromResult(new AuthenticationResult(true));
            }

            var response = GetAsync(sharedClient);
            var responseJson = JsonConvert.DeserializeObject<AuthResult>(response.Result.ToString());
            Console.WriteLine(responseJson.approved);
            if(responseJson.approved == true) {
                return Task.FromResult(new AuthenticationResult(true));
            }

            return Task.FromResult<AuthenticationResult>(new AuthenticationResult(false));
        }

        static async Task<string> GetAsync(HttpClient httpClient)
        {
            try {
                using HttpResponseMessage response = await httpClient.GetAsync("RequestAuthentication");
                            response.EnsureSuccessStatusCode()
                .WriteRequestToConsole();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"{jsonResponse}\n");
                return jsonResponse;

            } catch (HttpRequestException ex) {
                Console.WriteLine($"Not found: {ex.Message}");
            }
            return null;
        }

    }
}
