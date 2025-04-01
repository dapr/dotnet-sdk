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

using Microsoft.CodeAnalysis;

namespace Dapr.Workflow.Analyzers.Test;

public class WorkflowRegistrationAnalyzerTests
{
    public class Workflow
    {
        [Fact]
        public async Task VerifyWorkflowNotRegistered()
        {
            var testCode = @"
                using Dapr.Workflow;
                using System.Threading.Tasks;

                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                { 
                    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                    {
                        return new OrderResult(""Order processed"");
                    }
                }

                class UseWorkflow()
                {
                    public async Task RunWorkflow(DaprWorkflowClient client, OrderPayload order)
                    {
                        await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, order);
                    }
                }

                record OrderPayload { }
                record OrderResult(string message) { }
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR1001", DiagnosticSeverity.Warning)
                .WithSpan(17, 63, 17, 94).WithMessage("The workflow class 'OrderProcessingWorkflow' is not registered");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task VerifyWorkflowRegistered()
        {
            var testCode = @"
                using Dapr.Workflow;
                using System.Threading.Tasks;

                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                { 
                    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                    {
                        return new OrderResult(""Order processed"");
                    }
                }

                record OrderPayload { }
                record OrderResult(string message) { }
                ";

            var startupCode = @"
                using Dapr.Workflow;    
                using Microsoft.Extensions.DependencyInjection;

                internal static class Extensions
                {
                    public static void AddApplicationServices(this IServiceCollection services)
                    {
                        services.AddDaprWorkflow(options =>
                        {
                            options.RegisterWorkflow<OrderProcessingWorkflow>();
                        });
                    }
                }
                ";

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, startupCode);
        }
    }

    public class WorkflowActivity
    {
        [Fact]
        public async Task VerifyActivityNotRegistered()
        {
            var testCode = @"
                using Dapr.Workflow;
                using System.Threading.Tasks;

                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                { 
                    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                    {
                        await context.CallActivityAsync(nameof(NotifyActivity), new Notification(""Order received""));
                        return new OrderResult(""Order processed"");
                    }
                }

                record OrderPayload { } 
                record OrderResult(string message) { }
                record Notification { public Notification(string message) { } }
                class NotifyActivity { }
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR1002", DiagnosticSeverity.Warning)
                .WithSpan(9, 57, 9, 79).WithMessage("The workflow activity class 'NotifyActivity' is not registered");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task VerifyActivityRegistered()
        {
            var testCode = @"
                using Dapr.Workflow;
                using Microsoft.Extensions.DependencyInjection;
                using System.Threading.Tasks;

                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                { 
                    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                    { 
                        await context.CallActivityAsync(nameof(NotifyActivity), new Notification(""Order received""));
                        return new OrderResult(""Order processed"");
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
                ";

            var startupCode = @"
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
                ";

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, startupCode);
        }
    }
}
