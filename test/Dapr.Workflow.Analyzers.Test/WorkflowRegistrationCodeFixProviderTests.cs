// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Analyzers.Common;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class WorkflowRegistrationCodeFixProviderTests
{
    [Fact]
    public async Task RegisterWorkflow()
    {
        const string code = """
                                        using Dapr.Workflow;
                                        using Microsoft.Extensions.DependencyInjection;
                                        using System.Threading.Tasks;
                            
                                        public static class Program
                                        {
                                        public static void Main()
                                        {
                                            var services = new ServiceCollection();
                            
                                            services.AddDaprWorkflow(options =>
                                            {
                                            });
                                        }
                            
                                        private static async Task ScheduleWorkflow(DaprWorkflowClient client)
                                        {        
                                            await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, new OrderPayload());
                                        }
                                        }
                            
                                        class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                        { 
                                        public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                        {
                                            return Task.FromResult(new OrderResult("Order processed"));
                                        }
                                        }
                            
                                        record OrderPayload { }
                                        record OrderResult(string message) { }
                                        
                            """;

        const string expectedChangedCode = """
                                  
                                              using Dapr.Workflow;
                                              using Microsoft.Extensions.DependencyInjection;
                                              using System.Threading.Tasks;
                                  
                                              public static class Program
                                              {
                                              public static void Main()
                                              {
                                                  var services = new ServiceCollection();
                                  
                                                  services.AddDaprWorkflow(options =>
                                                  {
                                                      options.RegisterWorkflow<OrderProcessingWorkflow>();
                                                  });
                                              }
                                  
                                              private static async Task ScheduleWorkflow(DaprWorkflowClient client)
                                              {        
                                                  await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, new OrderPayload());
                                              }
                                              }
                                  
                                              class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                              { 
                                              public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                              {
                                                  return Task.FromResult(new OrderResult("Order processed"));
                                              }
                                              }
                                  
                                              record OrderPayload { }
                                              record OrderResult(string message) { }
                                              
                                  """;

        await VerifyCodeFix.RunTest<WorkflowRegistrationCodeFixProvider>(code, expectedChangedCode, typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }

    [Fact]
    public async Task RegisterWorkflow_WhenAddDaprWorkflowIsNotFound()
    {
        const string code = """       
                                        using Dapr.Workflow;
                                        using Microsoft.AspNetCore.Builder;
                                        using System.Threading.Tasks;
                            
                                        public static class Program
                                        {
                                        public static void Main()
                                        {
                                            var builder = WebApplication.CreateBuilder();
                            
                                            var app = builder.Build();
                                        }
                            
                                        private static async Task ScheduleWorkflow(DaprWorkflowClient client)
                                        {        
                                            await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, new OrderPayload());
                                        }
                                        }
                            
                                        class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                        { 
                                            public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                            {
                                                return Task.FromResult(new OrderResult("Order processed"));
                                            }
                                        }
                            
                                        record OrderPayload { }
                                        record OrderResult(string message) { }
                            """;

        const string expectedChangedCode = """
                                                       using Dapr.Workflow;
                                                       using Microsoft.AspNetCore.Builder;
                                                       using System.Threading.Tasks;
                                           
                                                       public static class Program
                                                       {
                                                       public static void Main()
                                                       {
                                                           var builder = WebApplication.CreateBuilder();
                                           
                                                           builder.Services.AddDaprWorkflow(options =>
                                                           {
                                                               options.RegisterWorkflow<OrderProcessingWorkflow>();
                                                           });
                                           
                                                           var app = builder.Build();
                                                       }
                                           
                                                       private static async Task ScheduleWorkflow(DaprWorkflowClient client)
                                                       {        
                                                           await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, new OrderPayload());
                                                       }
                                                       }
                                           
                                                       class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                                       { 
                                                       public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                       {
                                                           return Task.FromResult(new OrderResult("Order processed"));
                                                       }
                                                       }
                                           
                                                       record OrderPayload { }
                                                       record OrderResult(string message) { }     
                                           """;

        await VerifyCodeFix.RunTest<WorkflowRegistrationCodeFixProvider>(code, expectedChangedCode, typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }

    [Fact]
    public async Task RegisterWorkflow_WhenAddDaprWorkflowIsNotFound_TopLevelStatements()
    {
        const string code = """           
                                        using Dapr.Workflow;
                                        using Microsoft.AspNetCore.Builder;
                                        using Microsoft.Extensions.DependencyInjection;
                                        using System.Threading.Tasks;
                            
                                        var builder = WebApplication.CreateBuilder();
                                            
                                        var app = builder.Build();
                            
                                        var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
                                        await workflowClient.ScheduleNewWorkflowAsync(nameof(TestNamespace.OrderProcessingWorkflow), null, new OrderPayload());
                            
                                        namespace TestNamespace
                                        {
                                            class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                            { 
                                                public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                {
                                                    return Task.FromResult(new OrderResult("Order processed"));
                                                }
                                            }
                                        }
                            
                                        record OrderPayload { }
                                        record OrderResult(string message) { }
                            """;

        const string expectedChangedCode = """           
                                                       using Dapr.Workflow;
                                                       using Microsoft.AspNetCore.Builder;
                                                       using Microsoft.Extensions.DependencyInjection;
                                                       using System.Threading.Tasks;
                                           
                                                       var builder = WebApplication.CreateBuilder();
                                           
                                                       builder.Services.AddDaprWorkflow(options =>
                                                       {
                                                           options.RegisterWorkflow<TestNamespace.OrderProcessingWorkflow>();
                                                       });
                                                           
                                                       var app = builder.Build();
                                           
                                                       var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
                                                       await workflowClient.ScheduleNewWorkflowAsync(nameof(TestNamespace.OrderProcessingWorkflow), null, new OrderPayload());
                                           
                                                       namespace TestNamespace
                                                       {
                                                           class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                                           { 
                                                               public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                               {
                                                                   return Task.FromResult(new OrderResult("Order processed"));
                                                               }
                                                           }                
                                                       }
                                           
                                                       record OrderPayload { }
                                                       record OrderResult(string message) { }    
                                           """;

        await VerifyCodeFix.RunTest<WorkflowRegistrationCodeFixProvider>(code, expectedChangedCode, typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }
}
