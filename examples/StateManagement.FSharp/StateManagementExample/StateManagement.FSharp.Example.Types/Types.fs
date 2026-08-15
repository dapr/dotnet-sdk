#nowarn "FS3261"

namespace StateManagement.FSharp.Example

open Dapr.StateManagement

type Widget = { Size: string; Color: string }

[<StateStore("statestore")>]
type IWidgetStore =
    inherit IDaprStateStoreClient
