// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Analyzers.Common;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class WorkflowActivityRegistrationAnalyzerTests
{
    [Fact]
        public async Task VerifyActivityNotRegistered()
        {
            const string testCode = """
                                                    using Dapr.Workflow;
                                                    using System.Threading.Tasks;
                                    
                                                    class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                                    { 
                                                        public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                        {
                                                            await context.CallActivityAsync(nameof(NotifyActivity), new Notification("Order received"));
                                                            return new OrderResult("Order processed");
                                                        }
                                                    }
                                    
                                                    record OrderPayload { } 
                                                    record OrderResult(string message) { }
                                                    record Notification { public Notification(string message) { } }
                                                    class NotifyActivity { }                           
                                    """;

            var expected = VerifyAnalyzer.Diagnostic(WorkflowActivityRegistrationAnalyzer.WorkflowActivityRegistrationDescriptor)
                .WithSpan(8, 57, 8, 79).WithMessage("The workflow activity type 'NotifyActivity' is not registered with the dependency injection provider");

            var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
            await analyzer.VerifyAnalyzerAsync<WorkflowActivityRegistrationAnalyzer>(testCode, expected);
        }

        [Fact]
        public async Task VerifyActivityRegistered()
        {
            const string testCode = """
                                                    using Dapr.Workflow;
                                                    using Microsoft.Extensions.DependencyInjection;
                                                    using System.Threading.Tasks;
                                    
                                                    class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                                    { 
                                                        public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                        { 
                                                            await context.CallActivityAsync(nameof(NotifyActivity), new Notification("Order received"));
                                                            return new OrderResult("Order processed");
                                                        } 
                                                    }
                                    
                                                    record OrderPayload { } 
                                                    record OrderResult(string message) { }
                                                    record Notification(string Message);
                                    
                                                    class NotifyActivity : WorkflowActivity<Notification, object?>
                                                    {
                                    
                                                        public override Task<object?> RunAsync(WorkflowActivityContext context, Notification notification)
                                                        {
                                                            return Task.FromResult<object?>(null);
                                                        }
                                                    }
                                    """;

            const string startupCode = """
                                                       using Dapr.Workflow;    
                                                       using Microsoft.Extensions.DependencyInjection;
                                       
                                                       internal static class Extensions
                                                       {
                                                           public static void AddApplicationServices(this IServiceCollection services)
                                                           {
                                                               services.AddDaprWorkflow(options =>
                                                               {
                                                                   options.RegisterActivity<NotifyActivity>();
                                                               });
                                                           }
                                                       }             
                                       """;

            var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
            await analyzer.VerifyAnalyzerAsync<WorkflowActivityRegistrationAnalyzer>(testCode, startupCode);
        }
}
