// Learn more about F# at http://fsharp.org

open System
open IDemoActor.FSharp
open Dapr.Actors.Client
open Dapr.Actors

let run =
    async {
        printfn "Creating a Bank Actor"
        let bank = ActorProxy.Create<IBankActor>(ActorId.CreateRandom(), "DemoActor")
        let mutable cond = true
        while cond do
            let! balance = bank.GetAccountBalance() |> Async.AwaitTask

            printfn $"Balance for account '{balance.AccountId}' is '{balance.Balance:c}'."
            printfn $"Withdrawing '{10m:c}'..."

            try
                do! bank.Withdraw { Amount = 10m } |> Async.AwaitTask
            with
            // Due to quirks with Async.AwaitTask and aggregate exception, this horrible stuff seems necessary
            | :? AggregateException as ae ->
                for ex in ae.InnerExceptions do
                    match ex with
                    | :? ActorMethodInvocationException as amie ->
                        printfn $"Overdraft: {amie.Message}"
                    | _ -> ()
                cond <- false
    }

[<EntryPoint>]
let main argv =
    Async.RunSynchronously run
    0 // return an integer exit code
