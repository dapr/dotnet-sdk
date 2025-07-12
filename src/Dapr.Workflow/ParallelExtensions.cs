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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dapr.Workflow;

/// <summary>
/// Extension methods for <see cref="WorkflowContext"/> that provide high-level parallel processing primitives
/// with controlled concurrency.
/// </summary>
public static class ParallelExtensions
{
    /// <summary>
    /// Processes a collection of inputs in parallel with controlled concurrency using a streaming execution model.
    /// </summary>
    /// <typeparam name="TInput">The type of input items to process.</typeparam>
    /// <typeparam name="TResult">The type of result items returned by the task factory.</typeparam>
    /// <param name="context">The orchestration context.</param>
    /// <param name="inputs">The collection of inputs to process in parallel.</param>
    /// <param name="taskFactory">
    /// A function that creates a task for each input item. This function is called in the orchestration context
    /// to ensure all tasks are properly tracked by the durable task framework.
    /// </param>
    /// <param name="maxConcurrency">
    /// The maximum number of tasks to execute concurrently. Defaults to 5 if not specified.
    /// Must be greater than 0.
    /// </param>
    /// <returns>
    /// A task that completes when all input items have been processed. The result is an array containing
    /// the results in the same order as the input collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/>, <paramref name="inputs"/>, or <paramref name="taskFactory"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxConcurrency"/> is less than or equal to 0.
    /// </exception>
    /// <exception cref="AggregateException">
    /// Thrown when one or more tasks fail during execution. All task exceptions are collected and wrapped.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method uses a streaming execution model that maintains constant memory usage regardless of input size.
    /// Only <paramref name="maxConcurrency"/> tasks are active at any given time, with new tasks started as
    /// existing ones complete. This provides optimal resource utilization and prevents memory issues with large datasets.
    /// </para>
    /// <para>
    /// The method is fully deterministic for durable task orchestrations. All tasks are created in the orchestration
    /// context before any coordination logic begins, ensuring proper replay behavior. The framework records history
    /// events for each task creation, and during replay, all tasks complete immediately with their recorded results.
    /// </para>
    /// <para>
    /// If any task fails, the method will wait for all currently executing tasks to complete before throwing an
    /// <see cref="AggregateException"/> containing all failures.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var orderIds = new[] { "order1", "order2", "order3", "order4", "order5" };
    /// var results = await context.ProcessInParallelAsync(
    ///     orderIds,
    ///     orderId => context.CallActivityAsync&lt;OrderResult&gt;("ProcessOrder", orderId),
    ///     maxConcurrency: 3);
    /// </code>
    /// </para>
    /// </remarks>
    public static async Task<TResult[]> ProcessInParallelAsync<TInput, TResult>(
        this WorkflowContext context,
        IEnumerable<TInput> inputs,
        Func<TInput, Task<TResult>> taskFactory,
        int maxConcurrency = 5)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(taskFactory);
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Max concurrency must be greater than 0.");

        var inputList = inputs.ToList();
        if (inputList.Count == 0)
            return [];

        var results = new TResult[inputList.Count];
        var inFlightTasks = new Dictionary<Task<TResult>, int>(); // Task -> result index
        var inputIndex = 0;
        var completedCount = 0;
        var exceptions = new List<Exception>();

        // Start initial batch up to maxConcurrency
        while (inputIndex < inputList.Count && inFlightTasks.Count < maxConcurrency)
        {
            var task = taskFactory(inputList[inputIndex]);
            inFlightTasks[task] = inputIndex;
            inputIndex++;
        }

        // Process remaining items with streaming execution
        while (completedCount < inputList.Count)
        {
            var completedTask = await Task.WhenAny(inFlightTasks.Keys);
            var resultIndex = inFlightTasks[completedTask];

            try
            {
                results[resultIndex] = await completedTask;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            inFlightTasks.Remove(completedTask);
            completedCount++;

            // Start next task if more work remains
            if (inputIndex < inputList.Count)
            {
                var nextTask = taskFactory(inputList[inputIndex]);
                inFlightTasks[nextTask] = inputIndex;
                inputIndex++;
            }
        }

        // If any exceptions occurred, throw them as an aggregate
        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                $"One or more tasks failed during parallel processing. {exceptions.Count} out of {inputList.Count} tasks failed.",
                exceptions);
        }

        return results;
    }
}
