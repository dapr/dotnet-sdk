namespace Dapr.Actors.Next.Examples.Approvals

open System.Text.Json
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Interpreted

module CapabilityContext =
    let State (context: ActorCapabilityContext) : DynamicStateBag =
        context.Arguments.["state"] :?> DynamicStateBag

    let Payload (context: ActorCapabilityContext) : JsonElement =
        context.Arguments.["payload"] :?> JsonElement
