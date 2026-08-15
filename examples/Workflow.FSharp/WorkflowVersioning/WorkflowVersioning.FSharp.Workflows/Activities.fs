#nowarn "FS3261"
namespace WorkflowVersioning.FSharp.Workflows.VacationApproval.Activities

open System.Threading.Tasks
open Dapr.Workflow
open WorkflowVersioning.FSharp.Services

type EmailActivityInput = {
    To: string
    Message: string
}

type SendEmailActivity(emailSvc: IEmailService) =
    inherit WorkflowActivity<EmailActivityInput, obj>()

    override _.RunAsync(_: WorkflowActivityContext, input: EmailActivityInput) : Task<obj> =
        task {
            do! emailSvc.SendEmailAsync(input.To, input.Message)
            return Unchecked.defaultof<obj>
        }