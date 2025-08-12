﻿// ------------------------------------------------------------------------
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
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WorkflowParallelFanOut;

public sealed partial class ProcessOrderActivity(ILogger<ProcessOrderActivity> logger) : WorkflowActivity<OrderRequest, OrderResult>
{
    private static readonly Random Random = new();
    
    /// <summary>
    /// Override to implement async (non-blocking) workflow activity logic.
    /// </summary>
    /// <param name="context">Provides access to additional context for the current activity execution.</param>
    /// <param name="order">The deserialized activity input.</param>
    /// <returns>The output of the activity as a task.</returns>
    public override async Task<OrderResult> RunAsync(WorkflowActivityContext context, OrderRequest order)
    {
        LogProcessingOrder(logger, order.OrderId, order.ProductName);
        
        //Simulate processing time (between 100 and 2000ms)
        var processingTime = Random.Next(100, 2000);
        await Task.Delay(processingTime);
        
        //Simulate occasional failures (10% chance)
        var shouldFail = Random.Next(1, 101) <= 10;

        if (shouldFail)
        {
            LogOrderFailed(logger, order.OrderId);
            return new OrderResult(
                order.OrderId,
                IsProcessed: false,
                TotalAmount: 0,
                Status: "Failed - System Error",
                ProcessedAt: DateTime.UtcNow);
        }

        // Simulate inventory check
        var hasInventory = Random.Next(1, 101) <= 90; // 90% chance of having inventory
        if (!hasInventory)
        {
            LogInsufficientInventory(logger, order.OrderId);
            return new OrderResult(
                order.OrderId,
                IsProcessed: false,
                TotalAmount: 0, 
                Status: "Failed - Insufficient Inventory", 
                ProcessedAt: DateTime.UtcNow);
        }
        
        //Calculate total amount (with potential discount)
        var totalAmount = order.Quantity * order.Price;
        
        // Apply bulk discount for large orders
        if (order.Quantity >= 10)
        {
            totalAmount *= 0.9m; // 10% discount
            LogDiscountApplied(logger, order.OrderId);
        }
        
        LogSuccessfullyProcessedOrder(logger, order.OrderId, totalAmount);
        return new OrderResult(
            order.OrderId, 
            IsProcessed: true,
            TotalAmount: totalAmount,
            Status: "Processed Successfully",
            ProcessedAt: DateTime.UtcNow);
    }

    [LoggerMessage(LogLevel.Information, "Processing order {OrderId} for product {ProductName}")]
    static partial void LogProcessingOrder(ILogger logger, string orderId, string productName);

    [LoggerMessage(LogLevel.Warning, "Order {OrderId} failed during processing")]
    static partial void LogOrderFailed(ILogger logger, string orderId);

    [LoggerMessage(LogLevel.Warning, "Order {OrderId} failed - insufficient inventory")]
    static partial void LogInsufficientInventory(ILogger logger, string orderId);

    [LoggerMessage(LogLevel.Information, "Applied bulk discount to order {OrderId}")]
    static partial void LogDiscountApplied(ILogger logger, string orderId);

    [LoggerMessage(LogLevel.Information, "Successfully processed order {OrderId} with total amount {TotalAmount:c}")]
    static partial void LogSuccessfullyProcessedOrder(ILogger logger, string orderId, decimal totalAmount);
}
