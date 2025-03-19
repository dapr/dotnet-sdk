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

namespace Dapr.Workflow.Analyzers.Test;

public class WorkflowActivityRegistrationCodeFixProviderTests
{
    [Fact]
    public async Task VerifyWorkflowActivityRegistrationCodeFix()
    {
        var code = @"
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
            }

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

        var expectedChangedCode = @"
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
                        options.RegisterActivity<NotifyActivity>();
                    });
                }
            }

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

        await VerifyCodeFix.RunTest<WorkflowActivityRegistrationCodeFixProvider>(code, expectedChangedCode);
    }

    [Fact]
    public async Task VerifyWorkflowActivityRegistrationCodeFix_WhenAnotherActivityIsAlreadyRegistered()
    {
        var code = @"
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
                        options.RegisterActivity<AnotherActivity>();
                    });
                }
            }

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
            
            class NotifyActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }

            class AnotherActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }
            ";

        var expectedChangedCode = @"
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
                        options.RegisterActivity<AnotherActivity>();
                        options.RegisterActivity<NotifyActivity>();
                    });
                }
            }

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
            
            class NotifyActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }

            class AnotherActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }
            ";

        await VerifyCodeFix.RunTest<WorkflowActivityRegistrationCodeFixProvider>(code, expectedChangedCode);
    }

    [Fact]
    public async Task VerifyWorkflowActivityRegistrationCodeFix_GivenOtherOptions()
    {
        var code = @"
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
                        options.RegisterActivity<AnotherActivity>();
                        options.RegisterWorkflow<OrderProcessingWorkflow>();
                    });
                }
            }

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
            
            class NotifyActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }

            class AnotherActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }
            ";

        var expectedChangedCode = @"
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
                        options.RegisterActivity<AnotherActivity>();
                        options.RegisterWorkflow<OrderProcessingWorkflow>();
                        options.RegisterActivity<NotifyActivity>();
                    });
                }
            }

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
            
            class NotifyActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }

            class AnotherActivity : WorkflowActivity<Notification, object>
            {
                public override Task<object> RunAsync(WorkflowActivityContext context, Notification notification)
                {
                    return Task.FromResult<object>(null);
                }
            }
            ";

        await VerifyCodeFix.RunTest<WorkflowActivityRegistrationCodeFixProvider>(code, expectedChangedCode);
    }
}
