#nowarn "FS3261"
namespace Dapr.IntegrationTest.FSharp.Workflow.Versioning

open System
open Dapr.IntegrationTest.FSharp.Workflow.Versioning.Glue
open Dapr.Workflow.Versioning
open Microsoft.Extensions.DependencyInjection
open Xunit

type VersioningTests() =

    [<Fact>]
    member this.FSharp_workflow_versions_are_discovered() =
        let services = ServiceCollection()
        services.AddDaprWorkflowVersioning() |> ignore
        use provider = services.BuildServiceProvider()
        let registry = RegistryAccessor.GetWorkflowVersionRegistry(provider)
        Assert.True(registry.ContainsKey("FSharpVersionedWorkflow"))
        let versions = registry.["FSharpVersionedWorkflow"]
        Assert.True(versions.Count >= 2)
        Assert.True(versions |> Seq.exists (fun v -> v.EndsWith("FSharpVersionedWorkflowV1", StringComparison.Ordinal)))
        Assert.True(versions |> Seq.exists (fun v -> v.EndsWith("FSharpVersionedWorkflowV2", StringComparison.Ordinal)))
