#nowarn "FS3261"

namespace Dapr.IntegrationTest.FSharp.StateManagement

open Dapr.StateManagement

type Widget = { Size: string; Color: string }

[<StateStore("statestore")>]
type IWidgetStore =
    inherit IDaprStateStoreClient
