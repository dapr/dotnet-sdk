namespace Dapr.Actors.Next.Examples.Approvals

open System
open System.Collections.Generic

type DocumentTypeCard = {
    DocumentType: string
    Description: string
}

type SubmitDocument = {
    Requester: string
    Amount: decimal
    Parties: IReadOnlyList<string>
    SimulateChargeFailure: bool
}

type Decision = {
    Approver: string
    Note: string
}

type SettlementInput = {
    DocumentId: string
    DocumentType: string
    Requester: string
    Amount: decimal
    Parties: IReadOnlyList<string>
    SimulateChargeFailure: bool
}

type SettlementResult = {
    Settled: bool
    FinalState: string
}

type PartyNotification = {
    DocumentId: string
    Party: string
}

type ChargeRequest = {
    DocumentId: string
    Amount: decimal
    SimulateFailure: bool
}

type ReleaseRequest = {
    DocumentId: string
    Amount: decimal
}

type DocumentSignal = {
    DocumentId: string
    EventName: string
}
