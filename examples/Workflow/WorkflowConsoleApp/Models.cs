namespace WorkflowConsoleApp.Models 
{
    record OrderPayload(string Name, double TotalCost, int Quantity = 1);
    record InventoryRequest(string RequestId, string ItemName, int Quantity);
    record InventoryResult(bool Success, InventoryItem orderPayload);
    record PaymentRequest(string RequestId, string ItemName, int Amount, double Currency);
    record OrderResult(bool Processed);
    record InventoryItem(string Name, double PerItemCost, int Quantity);
}
