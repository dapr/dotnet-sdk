// // ------------------------------------------------------------------------
// // Copyright 2025 The Dapr Authors
// // Licensed under the Apache License, Version 2.0 (the "License");
// // you may not use this file except in compliance with the License.
// // You may obtain a copy of the License at
// //     http://www.apache.org/licenses/LICENSE-2.0
// // Unless required by applicable law or agreed to in writing, software
// // distributed under the License is distributed on an "AS IS" BASIS,
// // WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// // See the License for the specific language governing permissions and
// // limitations under the License.
// //  ------------------------------------------------------------------------
//
// using Dapr.Client;
// using Dapr.TestContainers;
// using Dapr.TestContainers.Common;
// using Dapr.TestContainers.Common.Options;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.AspNetCore.Http;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Hosting;
//
// namespace Dapr.E2E.Test.PubSub;
//
// public class PubsubTests
// {
//     [Fact]
//     public async Task ShouldEnablePublishAndSubscribe()
//     {
//         var options = new DaprRuntimeOptions();
//         var componentsDir = Path.Combine(Directory.GetCurrentDirectory(), "pubsub-components");
//         const string topicName = "test-topic";
//
//         WebApplication? app = null;
//         
//         var messageReceived = new TaskCompletionSource<string>();
//
//         // Build and initialize the test harness
//         var harnessBuilder = new DaprHarnessBuilder(options, StartApp);
//         var harness = harnessBuilder.BuildPubSub(componentsDir);
//
//         try
//         {
//             await harness.InitializeAsync();
//
//             var testAppBuilder = new HostApplicationBuilder();
//             testAppBuilder.Services.AddDaprClient();
//             using var testApp = testAppBuilder.Build();
//             await using var scope = testApp.Services.CreateAsyncScope();
//             var daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();
//
//             // Use DaprClient to publish a message
//             const string testMessage = "Hello!";
//             await daprClient.PublishEventAsync(Constants.DaprComponentNames.PubSubComponentName, topicName,
//                 testMessage);
//
//             // Wait for the app to receive the message via the sidecar
//             var result = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
//             Assert.Equal(testMessage, result);
//         }
//         finally
//         {
//             await harness.DisposeAsync();
//
//             if (app != null)
//                 await app.DisposeAsync();
//         }
//
//         return;
//
//         // Define the app startup
//         async Task StartApp(int port)
//         {
//             var builder = WebApplication.CreateBuilder();
//             builder.WebHost.UseUrls($"http://localhost:{port}");
//             builder.Services.AddControllers().AddDapr();
//
//             app = builder.Build();
//
//             // Setup the subscription endpoint
//             app.UseCloudEvents();
//             app.MapSubscribeHandler();
//
//             // Endpoint that Dapr will call when a message is published
//             app.MapPost("/message-handler", async (HttpContext context) =>
//                 {
//                     var data = await context.Request.ReadFromJsonAsync<dynamic>();
//                     messageReceived.TrySetResult(data?.ToString() ?? "empty");
//                     return Results.Ok();
//                 })
//                 .WithTopic(Constants.DaprComponentNames.PubSubComponentName, topicName);
//
//             await app.StartAsync();
//         }
//     }
//     
// }
