using Microsoft.AspNetCore.Mvc;

namespace WorkflowVersioning.Workflows.VacationApproval.Models;

public sealed record VacationRequest(
    [FromQuery(Name = "name")] string EmployeeName, 
    [FromQuery(Name = "start")] DateOnly StartDate,
    [FromQuery(Name = "end")] DateOnly EndDate);
