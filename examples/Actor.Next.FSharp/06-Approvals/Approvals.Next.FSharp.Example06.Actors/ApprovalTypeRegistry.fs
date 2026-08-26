namespace Dapr.Actors.Next.Examples.Approvals

open System
open System.Collections.Generic
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Interpreted

type ApprovalTypeRegistry() =
    let descriptor =
        ActorTypeDescriptor(
            ApprovalDefinitions.ActorType,
            1,
            typeof<InterpretedStateMachineActor>,
            typeof<InterpretedStateMachineActor>,
            [|
                ActorMethodDescriptor(
                    "Raise",
                    "Raise",
                    typeof<InterpretedRaiseResult>,
                    [|
                        ActorParameterDescriptor("evt", typeof<InterpretedEvent>, 0, false, false, null)
                    |])
            |])

    interface IActorRegistry with
        member _.Actors : IReadOnlyList<ActorTypeDescriptor> =
            upcast [| descriptor |]

        member _.TryGet(actorType: string, value: byref<ActorTypeDescriptor>) : bool =
            if String.Equals(actorType, ApprovalDefinitions.ActorType, StringComparison.Ordinal) then
                value <- descriptor
                true
            else
                value <- Unchecked.defaultof<_>
                false
