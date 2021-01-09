namespace DemoActor.FSharp.BankService

open IDemoActor.FSharp

// Annoyances:
//
// 1. Not really much, maybe just being a class?
type BankService() =
    let overdraftThreshold = -50m

    member _.Withdraw(balance: decimal, amount: decimal) =
        let updated = balance - amount

        if updated < overdraftThreshold then
            raise (OverdraftException(balance, amount))

        updated