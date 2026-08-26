namespace WorkflowVersioning.FSharp.Services

open System.Threading.Tasks

type IEmailService =
    abstract member SendEmailAsync: string * string -> Task

type EmailService() =
    interface IEmailService with
        member _.SendEmailAsync(_recipient: string, _body: string) =
            Task.CompletedTask