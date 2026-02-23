using Dapr.Workflow;
using WorkflowVersioning.Services;

namespace WorkflowVersioning.Workflows.VacationApproval.Activities;

public sealed class SendEmailActivity(IEmailService emailSvc) : WorkflowActivity<EmailActivityInput, object?>
{
    public override async Task<object?> RunAsync(WorkflowActivityContext context, EmailActivityInput input)
    {
        await emailSvc.SendEmailAsync(input.To, input.Message);
        return null;
    }
}

public sealed record EmailActivityInput(string To, string Message);
