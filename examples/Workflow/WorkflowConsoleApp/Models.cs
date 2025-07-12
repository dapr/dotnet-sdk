namespace WorkflowConsoleApp;

public record OrderPayload(string Name, double TotalCost, int Quantity = 1);
public record InventoryRequest(string RequestId, string ItemName, int Quantity);
public record InventoryResult(bool Success, InventoryItem orderPayload);
public record PaymentRequest(string RequestId, string ItemName, int Amount, double Currency);
public record OrderResult(bool Processed);
public record InventoryItem(string Name, double PerItemCost, int Quantity);
public enum ApprovalResult
{
    Unspecified = 0,
    Approved = 1,
    Rejected = 2,
}