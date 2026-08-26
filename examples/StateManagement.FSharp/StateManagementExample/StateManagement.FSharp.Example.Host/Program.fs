#nowarn "FS3261"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Dapr.StateManagement
open Dapr.StateManagement.Extensions
open Microsoft.Extensions.DependencyInjection
open StateManagement.FSharp.Example

let main () = task {
    let services = ServiceCollection()
    (services.AddDaprStateManagementClient()).WithWidgetStore() |> ignore

    use provider = services.BuildServiceProvider()
    let store = provider.GetRequiredService<IWidgetStore>()
    let client = provider.GetRequiredService<DaprStateManagementClient>()

    Console.WriteLine("=== Typed store (via source generator) ===")
    let key = "my-widget"

    let widget: Widget = { Size = "medium"; Color = "blue" }
    do! store.SaveStateAsync(key, widget)
    Console.WriteLine($"Saved: key={key}")

    let! loaded = store.GetStateAsync<Widget>(key)
    match box loaded with
    | null -> Console.WriteLine("Loaded: null")
    | _ -> Console.WriteLine($"Loaded: {loaded.Size} / {loaded.Color}")

    let! (existingValue, etag) = store.GetStateAndETagAsync<Widget>(key)
    if etag <> null then
        let updated = { existingValue with Color = "green" }
        let! saved = store.TrySaveStateAsync(key, updated, etag)
        Console.WriteLine(if saved then "ETag save succeeded." else "ETag mismatch.")

    do! store.DeleteStateAsync(key)
    Console.WriteLine($"Deleted key={key}")

    Console.WriteLine("=== Direct DaprStateManagementClient ===")
    let storeName = "statestore"

    do! client.SaveBulkStateAsync(storeName, ResizeArray<SaveStateItem<Widget>>(
        [
            SaveStateItem<Widget>("widget-a", { Size = "small"; Color = "red" })
            SaveStateItem<Widget>("widget-b", { Size = "large"; Color = "white" })
        ]))

    let! bulk = client.GetBulkStateAsync<Widget>(storeName, [|"widget-a"; "widget-b"|])
    for item in bulk do
        match box item.Value with
        | null -> Console.WriteLine($"Bulk item: key={item.Key}, value=null")
        | _ -> Console.WriteLine($"Bulk item: key={item.Key}, value={item.Value.Size}/{item.Value.Color}")

    do! client.ExecuteStateTransactionAsync(storeName, ResizeArray<StateTransactionRequest>(
        [
            StateTransactionRequest("widget-a", null, StateOperationType.Delete)
            StateTransactionRequest("widget-b", null, StateOperationType.Delete)
        ]))
    Console.WriteLine("Transaction committed.")
}

main().Wait()
