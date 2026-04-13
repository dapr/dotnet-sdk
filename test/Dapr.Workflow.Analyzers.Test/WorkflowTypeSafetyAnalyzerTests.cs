using Dapr.Analyzers.Common;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class WorkflowTypeSafetyAnalyzerTests
{
    [Fact]
    public async Task VerifyWorkflowInputTypeMismatch()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(DaprWorkflowClient client)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(OrderWorkflow), input: "wrong");
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.InputTypeMismatchDescriptor)
            .WithSpan(13, 71, 13, 85)
            .WithMessage(
                "The provided input type 'string' does not match the expected input type 'int' for workflow 'OrderWorkflow'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyWorkflowInputTypeMismatch_WhenUsingWorkflowClientInterface()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(IDaprWorkflowClient client)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(OrderWorkflow), input: "wrong");
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.InputTypeMismatchDescriptor)
            .WithSpan(13, 71, 13, 85)
            .WithMessage(
                "The provided input type 'string' does not match the expected input type 'int' for workflow 'OrderWorkflow'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyActivityInputTypeMismatch()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class NotifyActivity : WorkflowActivity<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class ParentWorkflow : Workflow<string, string>
                                {
                                    public override async Task<string> RunAsync(WorkflowContext context, string input)
                                    {
                                        return await context.CallActivityAsync<string>(nameof(NotifyActivity), "wrong");
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.InputTypeMismatchDescriptor)
            .WithSpan(13, 80, 13, 87)
            .WithMessage(
                "The provided input type 'string' does not match the expected input type 'int' for workflow activity 'NotifyActivity'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyActivityOutputTypeMismatch()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class NotifyActivity : WorkflowActivity<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class ParentWorkflow : Workflow<string, string>
                                {
                                    public override async Task<string> RunAsync(WorkflowContext context, string input)
                                    {
                                        int result = await context.CallActivityAsync<int>(nameof(NotifyActivity), 42);
                                        return result.ToString();
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.OutputTypeMismatchDescriptor)
            .WithSpan(13, 36, 13, 58)
            .WithMessage(
                "The requested output type 'int' does not match the declared output type 'string' for workflow activity 'NotifyActivity'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyChildWorkflowOutputTypeMismatch()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class ChildWorkflow : Workflow<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class ParentWorkflow : Workflow<int, string>
                                {
                                    public override async Task<string> RunAsync(WorkflowContext context, int input)
                                    {
                                        var result = await context.CallChildWorkflowAsync<int>(nameof(ChildWorkflow), input);
                                        return result.ToString();
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.OutputTypeMismatchDescriptor)
            .WithSpan(13, 36, 13, 63)
            .WithMessage(
                "The requested output type 'int' does not match the declared output type 'string' for workflow 'ChildWorkflow'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyCompatibleTypesDoNotReport()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public class Notification
                                {
                                }

                                public sealed class ImportantNotification : Notification
                                {
                                }

                                public sealed class NotifyActivity : WorkflowActivity<Notification, ImportantNotification>
                                {
                                    public override Task<ImportantNotification> RunAsync(WorkflowActivityContext context, Notification input) =>
                                        Task.FromResult(new ImportantNotification());
                                }

                                public sealed class ChildWorkflow : Workflow<Notification, ImportantNotification>
                                {
                                    public override Task<ImportantNotification> RunAsync(WorkflowContext context, Notification input) =>
                                        Task.FromResult(new ImportantNotification());
                                }

                                public sealed class ParentWorkflow : Workflow<ImportantNotification, Notification>
                                {
                                    public override async Task<Notification> RunAsync(WorkflowContext context, ImportantNotification input)
                                    {
                                        Notification activityResult = await context.CallActivityAsync<Notification>(nameof(NotifyActivity), input);
                                        Notification childResult = await context.CallChildWorkflowAsync<Notification>(nameof(ChildWorkflow), input);
                                        return activityResult ?? childResult;
                                    }
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(DaprWorkflowClient client, ImportantNotification input)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(ChildWorkflow), input: input);
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyWorkflowInputMismatch_WithStringLiteralName_DoesNotReport()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(DaprWorkflowClient client)
                                    {
                                        return client.ScheduleNewWorkflowAsync("OrderWorkflow", input: "wrong");
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyActivityInputMismatch_WithConstStringName_DoesNotReport()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class NotifyActivity : WorkflowActivity<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowActivityContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class ParentWorkflow : Workflow<string, string>
                                {
                                    private const string ActivityName = "NotifyActivity";

                                    public override async Task<string> RunAsync(WorkflowContext context, string input)
                                    {
                                        return await context.CallActivityAsync<string>(ActivityName, "wrong");
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyWorkflowInputNull_DoesNotReport()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<int, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, int input) => Task.FromResult(input.ToString());
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(DaprWorkflowClient client)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(OrderWorkflow), input: null);
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyUnrelatedScheduleNewWorkflowAsync_DoesNotReport()
    {
        const string testCode = """
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow
                                {
                                }

                                public sealed class FakeClient
                                {
                                    public Task ScheduleNewWorkflowAsync(string name, object input) => Task.CompletedTask;
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(FakeClient client)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(OrderWorkflow), "wrong");
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyUnrelatedCallActivityAsync_DoesNotReport()
    {
        const string testCode = """
                                using System.Threading.Tasks;

                                public sealed class FakeContext
                                {
                                    public Task<T> CallActivityAsync<T>(string name, object input) => Task.FromResult(default(T)!);
                                }

                                public sealed class ParentWorkflow
                                {
                                    public async Task<string> RunAsync(FakeContext context)
                                    {
                                        return await context.CallActivityAsync<string>(nameof(ParentWorkflow), "wrong");
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyUnrelatedCallChildWorkflowAsync_DoesNotReport()
    {
        const string testCode = """
                                using System.Threading.Tasks;

                                public sealed class FakeContext
                                {
                                    public Task<T> CallChildWorkflowAsync<T>(string workflowName, object input) => Task.FromResult(default(T)!);
                                }

                                public sealed class ChildWorkflow
                                {
                                }

                                public sealed class ParentWorkflow
                                {
                                    public async Task<string> RunAsync(FakeContext context, int input)
                                    {
                                        var result = await context.CallChildWorkflowAsync<int>(nameof(ChildWorkflow), input);
                                        return result.ToString();
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyActivityOutputDerivedRequestedFromBaseDeclared_Reports()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public class Notification
                                {
                                }

                                public sealed class ImportantNotification : Notification
                                {
                                }

                                public sealed class NotifyActivity : WorkflowActivity<int, Notification>
                                {
                                    public override Task<Notification> RunAsync(WorkflowActivityContext context, int input) =>
                                        Task.FromResult<Notification>(new Notification());
                                }

                                public sealed class ParentWorkflow : Workflow<string, string>
                                {
                                    public override async Task<string> RunAsync(WorkflowContext context, string input)
                                    {
                                        ImportantNotification result = await context.CallActivityAsync<ImportantNotification>(nameof(NotifyActivity), 42);
                                        return result.ToString();
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.OutputTypeMismatchDescriptor)
            .WithSpan(22, 54, 22, 94)
            .WithMessage(
                "The requested output type 'ImportantNotification' does not match the declared output type 'Notification' for workflow activity 'NotifyActivity'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyWorkflowInputCompatibleBaseType_DoesNotReport()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public class Notification
                                {
                                }

                                public sealed class ImportantNotification : Notification
                                {
                                }

                                public sealed class ChildWorkflow : Workflow<Notification, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, Notification input) =>
                                        Task.FromResult("ok");
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(DaprWorkflowClient client, ImportantNotification input)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(ChildWorkflow), input: input);
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyWorkflowTupleInputCompatible_DoesNotReport()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<(int Id, string Name), string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, (int Id, string Name) input)
                                        => Task.FromResult(input.Name);
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(DaprWorkflowClient client)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(OrderWorkflow), input: (1, "test"));
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }
    
    [Fact]
    public async Task VerifyWorkflowTupleInputMismatch_Reports()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<(int Id, string Name), string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, (int Id, string Name) input)
                                        => Task.FromResult(input.Name);
                                }

                                public sealed class WorkflowStarter
                                {
                                    public Task StartAsync(DaprWorkflowClient client)
                                    {
                                        return client.ScheduleNewWorkflowAsync(nameof(OrderWorkflow), input: ("wrong", "test"));
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.InputTypeMismatchDescriptor)
            .WithSpan(14, 71, 14, 95)
            .WithMessage("The provided input type '(string, string)' does not match the expected input type '(int Id, string Name)' for workflow 'OrderWorkflow'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }    
    
    [Fact]
    public async Task VerifyActivityTupleOutputCompatible_DoesNotReport()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class NotifyActivity : WorkflowActivity<int, (int Code, string Message)>
                                {
                                    public override Task<(int Code, string Message)> RunAsync(WorkflowActivityContext context, int input)
                                        => Task.FromResult((input, "ok"));
                                }

                                public sealed class ParentWorkflow : Workflow<string, string>
                                {
                                    public override async Task<string> RunAsync(WorkflowContext context, string input)
                                    {
                                        var result = await context.CallActivityAsync<(int Code, string Message)>(nameof(NotifyActivity), 42);
                                        return result.Message;
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode);
    }
    
    [Fact]
    public async Task VerifyActivityTupleOutputMismatch_Reports()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class NotifyActivity : WorkflowActivity<int, (int Code, string Message)>
                                {
                                    public override Task<(int Code, string Message)> RunAsync(WorkflowActivityContext context, int input)
                                        => Task.FromResult((input, "ok"));
                                }

                                public sealed class ParentWorkflow : Workflow<string, string>
                                {
                                    public override async Task<string> RunAsync(WorkflowContext context, string input)
                                    {
                                        var result = await context.CallActivityAsync<(string Code, string Message)>(nameof(NotifyActivity), 42);
                                        return result.Message;
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.OutputTypeMismatchDescriptor)
            .WithSpan(14, 36, 14, 84)
            .WithMessage("The requested output type '(string Code, string Message)' does not match the declared output type '(int Code, string Message)' for workflow activity 'NotifyActivity'");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(testCode, expected);
    }
}
