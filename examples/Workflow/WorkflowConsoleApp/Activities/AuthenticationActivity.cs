using System.Text;
using System.Text.Json;
using Dapr.Client;
using Dapr.Workflow;
using Microsoft.Extensions.Logging;
using WorkflowConsoleApp.Models;

namespace WorkflowConsoleApp.Activities
{

    public class AuthenticationActivity : WorkflowActivity<InventoryRequest, AuthenticationResult>
    {
        readonly ILogger logger;
        private static readonly HttpClient client = new HttpClient();
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


            // Get attempt. Maybe we want to use a get rather than a post for reaching the server?
            var getRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.getpostman.com/collections");
            getRequest.Headers.Add("x-api-key", "API KEY GOES HERE");
            using var getResponse = client.SendAsync(getRequest);
            this.logger.LogInformation(
                        "Response from client get call '{getResponse}'",
                        getResponse.Result.ToString()
            );


            // Post attempt.
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.getpostman.com/mocks");
            //IMPORTANT!!! Hide this key in an environment variable instead of hardcoding it
            request.Headers.Add("x-api-key", "API KEY GOES HERE");
            string jsonText = JsonSerializer.Serialize(new
            {
                name = "testAPImock",
                collection = "COLLECTION VALUE GOES HERE", 
                environment = "ENVIRONMENT VALUE GOES HERE",
            });
            request.Content = new StringContent(jsonText, Encoding.UTF8, "application/json");
            using var response = client.SendAsync(request);
            // Do something with the response...

            this.logger.LogInformation(
                        "Response from client call '{response}'",
                        response.Result.ToString()
            );

            // Probably want to change this up to have a different type of exception thrown and compare against something other than a hardcoded 400
            if (Convert.ToInt32(response.Result.StatusCode) == 400)
            {
                throw new ArgumentNullException(paramName: nameof(response), message: "server is unable to be reached.");
            }

            var authenticationResult = new AuthenticationResult(true);

            return Task.FromResult<AuthenticationResult>(authenticationResult);
        }
    }
}
