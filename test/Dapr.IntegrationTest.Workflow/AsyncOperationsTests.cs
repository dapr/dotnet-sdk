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
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.DependencyInjection;
//
// namespace Dapr.IntegrationTest.Workflow;
//
// public sealed class AsyncOperationsTests
// {
//     private const string ProcessingPaymentStatus = "Processing payment...";
//     private const string ContactingWarehouseStatus = "Contacting warehouse...";
//     private const string SuccessStatus = "Success!";
//     
//     [Fact]
//     public async Task ShouldHandleAsyncOperations()
//     {
//         var options = new DaprRuntimeOptions();
//         var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
//         var workflowInstanceId = Guid.NewGuid().ToString();
//         
//         var harness = new DaprHarnessBuilder(options).BuildWorkflow(componentsDir);
//         await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
//             .ConfigureServices(builder =>
//             {
//                 builder.Services.AddDaprWorkflowBuilder(
//                     configureRuntime: opt =>
//                     {
//                         opt.RegisterWorkflow<TestWorkflow>();
//                         opt.RegisterActivity<NotifyWarehouseActivity>();
//                         opt.RegisterActivity<ProcessPaymentActivity>();
//                     },
//                     configureClient: (sp, clientBuilder) =>
//                     {
//                         var config = sp.GetRequiredService<IConfiguration>();
//                         var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
//                         if (!string.IsNullOrEmpty(grpcEndpoint))
//                             clientBuilder.UseGrpcEndpoint(grpcEndpoint);
//                     });
//             })
//             .BuildAndStartAsync();
//
//         // Clean test logic
//         using var scope = testApp.CreateScope();
//         var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
//
//         // Start the workflow
//         var transaction = new Transaction(15.47m);
//         await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(TestWorkflow), workflowInstanceId, transaction);
//         
//         // Wait a second and then get the custom status
//         await Task.Delay(TimeSpan.FromSeconds(1));
//         var status1 = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
//         Assert.NotNull(status1);
//         var status1Value = status1?.ReadCustomStatusAs<string>();
//         Assert.Equal(ProcessingPaymentStatus, status1Value);
//
//         // The first operation elapses after 5 seconds, so check at a total of 7 seconds
//         await Task.Delay(TimeSpan.FromSeconds(6));
//         var status2 = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
//         Assert.NotNull(status2);
//         var status2Value = status2?.ReadCustomStatusAs<string>();
//         Assert.Equal(ContactingWarehouseStatus, status2Value);
//
//         // The second operation elapses after 10 seconds, so check at a total of 13 seconds
//         await Task.Delay(TimeSpan.FromSeconds(6));
//         var status3 = await daprWorkflowClient.GetWorkflowStateAsync(workflowInstanceId);
//         Assert.NotNull(status3);
//         var status3Value = status3?.ReadCustomStatusAs<string>();
//         Assert.Equal(SuccessStatus, status3Value);
//     }   
//
//     private sealed record Transaction(decimal Value)
//     {
//         public Guid CustomerId { get; init; } = Guid.NewGuid();
//     }
//     
//     private sealed class TestWorkflow : Workflow<Transaction, bool>
//     {
//         public override async Task<bool> RunAsync(WorkflowContext context, Transaction input)
//         {
//             try
//             {
//                 // Submit the transaction to the payment processor
//                 context.SetCustomStatus(ProcessingPaymentStatus);
//                 await context.CallActivityAsync(nameof(ProcessPaymentActivity), input);
//
//                 // Send the transaction details to the warehouse
//                 context.SetCustomStatus(ContactingWarehouseStatus);
//                 await context.CallActivityAsync(nameof(NotifyWarehouseActivity), input);
//
//                 context.SetCustomStatus(SuccessStatus);
//                 return true;
//             }
//             catch
//             {
//                 // If anything goes wrong, return false
//                 context.SetCustomStatus("Something went wrong!");
//                 return false;
//             }
//         }
//     }
//
//     private sealed class ProcessPaymentActivity : WorkflowActivity<Transaction, object?>
//     {
//         public override async Task<object?> RunAsync(WorkflowActivityContext context, Transaction input)
//         {
//             await Task.Delay(TimeSpan.FromSeconds(10));
//             return null;
//         }
//     }
//
//     private sealed class NotifyWarehouseActivity : WorkflowActivity<Transaction, object?>
//     {
//         public override async Task<object?> RunAsync(WorkflowActivityContext context, Transaction input)
//         {
//             await Task.Delay(TimeSpan.FromSeconds(5));
//             return null;
//         }
//     }
// }
