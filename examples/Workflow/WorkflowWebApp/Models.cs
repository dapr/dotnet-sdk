namespace WorkflowWebApp.Models 
{
    record OrderPayload(string Name, double TotalCost, int Quantity = 1);
    record InventoryRequest(string RequestId, string ItemName, int Quantity);
    record InventoryResult(bool Success, OrderPayload orderPayload, string etag);
    record PaymentRequest(string RequestId, string ItemBeingPruchased, int Amount, double Currency);
    record OrderResult(bool Processed);

}