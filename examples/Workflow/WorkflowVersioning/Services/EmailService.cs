namespace WorkflowVersioning.Services;

public sealed class EmailService : IEmailService
{
    public Task SendEmailAsync(string to, string body)
    {
        // No-op
        return Task.CompletedTask;
    }
}
