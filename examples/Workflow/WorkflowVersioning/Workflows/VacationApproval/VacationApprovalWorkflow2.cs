using Dapr.Workflow;
using WorkflowVersioning.Workflows.VacationApproval.Activities;
using WorkflowVersioning.Workflows.VacationApproval.Models;

namespace WorkflowVersioning.Workflows.VacationApproval;

public sealed class VacationApprovalWorkflow2 : Workflow<VacationRequest, bool>
{
    public override async Task<bool> RunAsync(WorkflowContext context, VacationRequest input)
    {
        var logger = context.CreateReplaySafeLogger<VacationApprovalWorkflow2>();
        
        // Only send the approval if this is at least two weeks out
        var now = context.CurrentUtcDateTime;
        if (input.StartDate < new DateOnly(now.Year, now.Month, now.Day).AddDays(14))
        {
            // Need at least two weeks of notice
            return false;
        }
        
        // Send approval email to the manager
        logger.LogInformation("Sending approval email to manager for workflow '{workflowId}'", context.InstanceId);
        await context.CallActivityAsync(nameof(SendEmailActivity),
            new EmailActivityInput("manager@localhost",
                $"Vacation request '{context.InstanceId}' from {input.EmployeeName} from {input.StartDate:d} to {input.EndDate:d}"));
        
        // Refactored the following and fixed a bug:
        // 1) If the approval is rejected, still approves if the external event doesn't time out
        
        // Wait for approval and respond accordingly
        bool approvalResponse;
        var denialMessage =
            $"Vacation request '{context.InstanceId}' denied from {input.StartDate:d} to {input.EndDate:d}"; 
        try
        {
            logger.LogInformation("Waiting for approval for workflow '{workflowId}'", context.InstanceId);
            approvalResponse = await context.WaitForExternalEventAsync<bool>("Approval", timeout: TimeSpan.FromSeconds(120));
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Approval timeout for workflow '{workflowId}'", context.InstanceId);
            await context.CallActivityAsync(nameof(SendEmailActivity),
                new EmailActivityInput($"{input.EmployeeName}@localhost", denialMessage));
            return false;
        }

        var approvalMessage =
            $"Vacation request '{context.InstanceId}' approved from {input.StartDate:d} to {input.EndDate:d}";
        logger.LogInformation("Received approval decision for workflow '{workflowId}', status: '{status}'", context.InstanceId, approvalResponse ? "Approved" : "Denied");
        var emailInput = new EmailActivityInput($"{input.EmployeeName}@localhost", approvalResponse ? approvalMessage : denialMessage);
        await context.CallActivityAsync(nameof(SendEmailActivity), emailInput);
        
        return approvalResponse;
    }
}
