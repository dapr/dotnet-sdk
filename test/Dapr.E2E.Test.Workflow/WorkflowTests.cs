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
// using Dapr.TestContainers.Common;
// using Dapr.TestContainers.Common.Options;
// using Dapr.Workflow;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
//
// namespace Dapr.E2E.Test.Workflow;
//
// public class WorkflowTests
// {
//     [Fact]
//     public async Task ShouldTestTaskChaining()
//     {
//         var options = new DaprRuntimeOptions();
//         var componentsDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-components-{Guid.NewGuid():N}");
//
//         WebApplication? app = null;
//         
//         // Build and initialize the test harness
//         var harnessBuilder = new DaprHarnessBuilder(options, StartApp);
//         var harness = harnessBuilder.BuildWorkflow(componentsDir);
//
//         try
//         {
//             await harness.InitializeAsync();
//             
//             await using var scope = app!.Services.CreateAsyncScope();
//             var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
//             
//             // Start the workflow
//             var workflowId = Guid.NewGuid().ToString("N");
//             const int startingValue = 8;
//             
//             await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TestWorkflow), workflowId, startingValue);
//             
//             var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowId, true);
//
//             Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
//             var resultValue = result.ReadOutputAs<int>();
//
//             Assert.Equal(16, resultValue);
//         }
//         finally
//         {
//             await harness.DisposeAsync();
//             if (app is not null)
//                 await app.DisposeAsync();
//         }
//
//         return;
//         
//         // Define the app startup
//         async Task StartApp(int port)
//         {
//             var builder = WebApplication.CreateBuilder();
//             builder.Logging.ClearProviders();
//             builder.Logging.AddSimpleConsole();
//             builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
//             builder.Services.AddDaprWorkflow(opt =>
//             {
//                 opt.RegisterWorkflow<TestWorkflow>();
//                 opt.RegisterActivity<DoublingActivity>();
//             });
//             
//             Console.WriteLine($"HTTP: {Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT")}");
//             Console.WriteLine($"GRPC: {Environment.GetEnvironmentVariable("DAPR_GRPC_ENDPOINT")}");
//
//             app = builder.Build();
//             await app.StartAsync();
//         }
//     }
//
//     private sealed class DoublingActivity : WorkflowActivity<int, int>
//     {
//         public override Task<int> RunAsync(WorkflowActivityContext context, int input)
//         {
//             var square = input * 2;
//             return Task.FromResult(square);
//         }
//     }
//
//     private sealed class TestWorkflow : Workflow<int, int>
//     {
//         public override async Task<int> RunAsync(WorkflowContext context, int input)
//         {
//             var result = await context.CallActivityAsync<int>(nameof(DoublingActivity), input);
//             return result;
//         }
//     }
// }
