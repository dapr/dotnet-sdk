#nowarn "FS3261"
namespace WorkflowConsoleApp

type OrderPayload = {
    Name: string
    TotalCost: float
    Quantity: int
}

type InventoryRequest = {
    RequestId: string
    ItemName: string
    Quantity: int
}

type InventoryItem = {
    Name: string
    PerItemCost: float
    Quantity: int
}

type InventoryResult = {
    Success: bool
    OrderPayload: InventoryItem
}

type PaymentRequest = {
    RequestId: string
    ItemName: string
    Amount: int
    Currency: float
}

type OrderResult = {
    Processed: bool
}

type Notification = {
    Message: string
}

type ApprovalResult =
    | Unspecified = 0
    | Approved = 1
    | Rejected = 2