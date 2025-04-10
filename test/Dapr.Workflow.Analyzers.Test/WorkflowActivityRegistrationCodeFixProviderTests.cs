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

public sealed class WorkflowActivityRegistrationCodeFixProviderTests
{
    [Fact]
    public async Task VerifyWorkflowActivityRegistrationCodeFix()
    {
        const string code = """
                                        using Dapr.Workflow;
                                        using System;
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
                                        }
                            
                                        class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                        { 
                                            public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                            {
                                                await context.CallActivityAsync(nameof(NotifyActivity), new Notification("Order received"));
                                                return new OrderResult("Order processed");
                                            }
                                        }
                            
                                        record OrderPayload;
                                        record OrderResult(string Message) { };
                                        record Notification { public Notification(string message) { } };
                                        internal sealed class NotifyActivity : WorkflowActivity<string, bool> 
                                        {
                                             public override async Task<bool> RunAsync(WorkflowActivityContext context, string input) 
                                             {
                                                 await Task.Delay(TimeSpan.FromSeconds(15));
                                                 return true;
                                             }
                                        }
                            """;

        const string expectedChangedCode = """
                                                       using Dapr.Workflow;
                                                       using System;
                                                       using Microsoft.Extensions.DependencyInjection;
                                                       using System.Threading.Tasks;
                                           
                                                       public static class Program
                                                       {
                                                           public static void Main()
                                                           {
                                                               var services = new ServiceCollection();
                                           
                                                               services.AddDaprWorkflow(options =>
                                                               {
                                                                   options.RegisterActivity<NotifyActivity>();
                                                               });
                                                           }
                                                       }
                                           
                                                       class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                                       { 
                                                           public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                           {
                                                               await context.CallActivityAsync(nameof(NotifyActivity), new Notification("Order received"));
                                                               return new OrderResult("Order processed");
                                                           }
                                                       }
                                           
                                                       record OrderPayload;
                                                       record OrderResult(string Message) { };
                                                       record Notification { public Notification(string message) { } };
                                                       internal sealed class NotifyActivity : WorkflowActivity<string, bool> 
                                                       {
                                                            public override async Task<bool> RunAsync(WorkflowActivityContext context, string input) 
                                                            {
                                                                await Task.Delay(TimeSpan.FromSeconds(15));
                                                                return true;
                                                            }
                                                       }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowActivityRegistrationCodeFixProvider>(code, expectedChangedCode, typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }
}
