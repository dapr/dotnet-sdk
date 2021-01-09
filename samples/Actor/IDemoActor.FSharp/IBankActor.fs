namespace IDemoActor.FSharp

open System
open System.Threading.Tasks
open Dapr.Actors

// Q: How to make better?
// A: Use a serialize that understands F# records and unions, e.g. FSharp.SystemTextJson
[<CLIMutable>]
type AccountBalance = { AccountId: string; Balance: decimal }

[<CLIMutable>]
type WithdrawRequest = { Amount: decimal }

// This is annoying as a library author, but 100% as a library consumer
type OverdraftException(balance: decimal, amount: decimal) =
    inherit Exception($"Your current balance is {balance:c} - that's not enough to withdraw {amount:c}.")

// This is straightforward stuff for F#, except for the marker interface stuff
type IBankActor =
    inherit IActor

    abstract GetAccountBalance: unit -> Task<AccountBalance>

    abstract Withdraw: WithdrawRequest -> Task