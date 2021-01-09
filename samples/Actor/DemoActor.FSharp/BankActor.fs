namespace DemoActor.FSharp.BankActor

open System.Threading.Tasks

open DemoActor.FSharp.BankService
open Dapr.Actors.Runtime
open IDemoActor.FSharp

// Main things that make this annoying:
//
// 0. Protected members can't be consumed, overridded, etc. Needs to be made public for F# users.
// 1. Inheritance kinda lame, but workable so long as we don't override stuff
// 2. Typical F# async <-> System.Thread.Tasks impedence mismatches
//
// How to make it better
//
// 0. Anything that is protected should be made public
// 1. ??? - but overriding is where it gets icky - moving away from inheritance-based stuff is very hard
// 2. F# 6 might solve this, if not, the library "Ply" (https://www.nuget.org/packages/Ply/) does.
type BankActor(host: ActorHost, bank: BankService) =
    inherit Actor(host)

    interface IBankActor with
        member this.GetAccountBalance() =
            async {
                let starting = { AccountId = this.Id.GetId(); Balance = 100m }
                let! balance = this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting) |> Async.AwaitTask
                return balance
            } |> Async.StartAsTask

            // Using Ply or (possibly) F# vNext: all is reasonable
            //
            // task {
            //     let starting = { AccountId = this.Id.GetId(); Balance = 100m }
            //     let! balance = this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting)
            //     return balance
            // }

        member this.Withdraw(withdraw: WithdrawRequest) =
            async {
                let starting = { AccountId = this.Id.GetId(); Balance = 100m }
                let! balance = this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting) |> Async.AwaitTask
                let updated = bank.Withdraw(balance.Balance, withdraw.Amount)
                return { balance with Balance = updated }
            } |> Async.StartAsTask :> Task // Extra annoying

            
            // Using Ply or (possibly) F# vNext: all is reasonable
            //
            // unitTask {
            //     let starting = { AccountId = this.Id.GetId(); Balance = 100m }
            //     let! balance = this.StateManager.GetOrAddStateAsync<AccountBalance>("balance", starting)
            //     let updated = bank.Withdraw(balance.Balance, withdraw.Amount)     
            //     return { balance with Balance = updated }
            // }