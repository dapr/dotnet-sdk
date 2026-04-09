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
            .WithMessage("The provided input type 'string' does not match the expected input type 'int' for workflow 'OrderWorkflow'");

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
            .WithMessage("The provided input type 'string' does not match the expected input type 'int' for workflow 'OrderWorkflow'");

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
            .WithMessage("The provided input type 'string' does not match the expected input type 'int' for workflow activity 'NotifyActivity'");

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
            .WithMessage("The requested output type 'int' does not match the declared output type 'string' for workflow activity 'NotifyActivity'");

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
            .WithMessage("The requested output type 'int' does not match the declared output type 'string' for workflow 'ChildWorkflow'");

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
}
