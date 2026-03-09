using Dapr.Workflow;
using WorkflowVersioning.Workflows.VacationApproval.Activities;
using WorkflowVersioning.Workflows.VacationApproval.Models;

namespace WorkflowVersioning.Workflows.VacationApproval;

public sealed class VacationApprovalWorkflow : Workflow<VacationRequest, bool>
{
    public override async Task<bool> RunAsync(WorkflowContext context, VacationRequest input)
    {
        // Oops - only send the approval if this is at least two weeks out
        if (context.IsPatched("needs-two-weeks-notice"))
        {
            var now = context.CurrentUtcDateTime;
            if (input.StartDate < new DateOnly(now.Year, now.Month, now.Day).AddDays(14))
            {
                // Need at least two weeks of notice
                return false;
            }
        }
        
        // Send approval email to the manager
        await context.CallActivityAsync(nameof(SendEmailActivity),
            new EmailActivityInput("manager@localhost",
                $"Vacation request '{context.InstanceId}' from {input.EmployeeName} from {input.StartDate:d} to {input.EndDate:d}"));
        
        // Wait for approval
        try
        {
            await context.WaitForExternalEventAsync<bool>("Approval", timeout: TimeSpan.FromSeconds(120));
        }
        catch (TaskCanceledException)
        {
            await context.CallActivityAsync(nameof(SendEmailActivity),
                new EmailActivityInput($"{input.EmployeeName}@localhost",
                    $"Vacation request '{context.InstanceId}' denied from {input.StartDate:d} to {input.EndDate:d}"));
            return false;
        }

        await context.CallActivityAsync(nameof(SendEmailActivity),
            new EmailActivityInput($"{input.EmployeeName}@localhost",
                $"Vacation request '{context.InstanceId}' approved from {input.StartDate:d} to {input.EndDate:d}"));
        return true;
    }
}
