#nowarn "FS3261"
namespace Dapr.IntegrationTest.FSharp.Workflow.Versioning.Workflows

open System.Threading.Tasks
open Dapr.Workflow
open Dapr.Workflow.Versioning

[<WorkflowVersion(CanonicalName = "FSharpVersionedWorkflow", Version = "1")>]
type FSharpVersionedWorkflowV1() =
    inherit Workflow<string, string>()
    override _.RunAsync(_: WorkflowContext, input: string) : Task<string> =
        Task.FromResult<string>($"v1:{input}")

[<WorkflowVersion(CanonicalName = "FSharpVersionedWorkflow", Version = "2")>]
type FSharpVersionedWorkflowV2() =
    inherit Workflow<string, string>()
    override _.RunAsync(_: WorkflowContext, input: string) : Task<string> =
        Task.FromResult<string>($"v2:{input}")
