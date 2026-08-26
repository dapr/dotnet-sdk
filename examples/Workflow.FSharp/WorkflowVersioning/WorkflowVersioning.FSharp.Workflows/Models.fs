namespace WorkflowVersioning.FSharp.Workflows.VacationApproval.Models

open System

type VacationRequest = {
    EmployeeName: string
    StartDate: DateOnly
    EndDate: DateOnly
}