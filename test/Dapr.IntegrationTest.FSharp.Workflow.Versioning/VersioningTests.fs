#nowarn "FS3261"
namespace Dapr.IntegrationTest.FSharp.Workflow.Versioning

open System
open Dapr.IntegrationTest.FSharp.Workflow.Versioning.Glue
open Dapr.IntegrationTest.FSharp.Workflow.Versioning.Workflows
open Dapr.Workflow
open Dapr.Workflow.Versioning
open Microsoft.Extensions.DependencyInjection
open Xunit

type VersioningTests() =

    [<Fact>]
    member _.FSharp_workflow_versions_are_discovered_and_executable() = task {
        let services = ServiceCollection()
        services.AddDaprWorkflowVersioning() |> ignore
        use provider = services.BuildServiceProvider()
        let registry = RegistryAccessor.GetWorkflowVersionRegistry(provider)
        Assert.True(registry.ContainsKey("FSharpVersionedWorkflow"))
        let versions = registry.["FSharpVersionedWorkflow"]
        Assert.True(versions.Count >= 2)
        Assert.True(versions |> Seq.exists (fun v -> v.EndsWith("FSharpVersionedWorkflowV1", StringComparison.Ordinal)))
        Assert.True(versions |> Seq.exists (fun v -> v.EndsWith("FSharpVersionedWorkflowV2", StringComparison.Ordinal)))

        let latestWorkflowName = versions.[0].Replace("global::", String.Empty)
        let latestWorkflowType = typeof<FSharpVersionedWorkflowV2>.Assembly.GetType(latestWorkflowName)
        Assert.NotNull(latestWorkflowType)

        let workflow = Activator.CreateInstance(latestWorkflowType) :?> Workflow<string, string>
        let! output = workflow.RunAsync(Unchecked.defaultof<WorkflowContext>, "smoke")
        Assert.Equal("v2:smoke", output)
    }
