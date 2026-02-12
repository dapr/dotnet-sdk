namespace WorkflowVersioning.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string body);
}
