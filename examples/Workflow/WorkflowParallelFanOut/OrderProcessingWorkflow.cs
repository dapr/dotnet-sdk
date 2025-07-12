// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Workflow;
using Microsoft.Extensions.Logging;

namespace WorkflowParallelFanOut;

public sealed partial class OrderProcessingWorkflow : Workflow<OrderRequest[], OrderResult[]>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="orders">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<OrderResult[]> RunAsync(WorkflowContext context, OrderRequest[] orders)
    {
        var logger = context.CreateReplaySafeLogger<OrderProcessingWorkflow>();

        if (!context.IsReplaying)
        {
            LogStartingOrderProcessorWorkflow(logger, orders.Length);
        }
        
        //Process all orders in parallel with controlled concurrency
        var orderResults = await context.ProcessInParallelAsync(
            orders,
            order => context.CallActivityAsync<OrderResult>(nameof(ProcessOrderActivity), order), maxConcurrency: 5);
        
        //Calculate summary statistics
        var totalProcessed = orderResults.Count(r => r.IsProcessed);
        var totalFailed = orderResults.Length - totalProcessed;
        var totalAmount = orderResults.Where(r => r.IsProcessed).Sum(r => r.TotalAmount);

        if (!context.IsReplaying)
        {
            LogCompletedProcessingWorkflow(logger, orders.Length, totalProcessed, totalFailed, totalAmount);
        }

        return orderResults;
    }

    [LoggerMessage(LogLevel.Information, "Starting order processing workflow with {OrderCount} orders")]
    static partial void LogStartingOrderProcessorWorkflow(ILogger logger, int orderCount);
    
    [LoggerMessage(LogLevel.Information, "Completed processing {TotalOrders} orders. Processed: {ProcessedCount}, Failed: {FailedCount}, Total Amount: {TotalAmount:c}")]
    static partial void LogCompletedProcessingWorkflow(ILogger logger, int totalOrders, int processedCount, int failedCount, decimal totalAmount);
}
