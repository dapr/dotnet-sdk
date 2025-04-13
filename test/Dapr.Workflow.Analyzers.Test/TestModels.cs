namespace Dapr.Workflow.Analyzers.Test;

internal record OrderPayload(string OrderId, string CustomerId);
internal record class OrderResult(string Result);
