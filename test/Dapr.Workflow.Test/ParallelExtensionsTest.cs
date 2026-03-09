// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Moq;

namespace Dapr.Workflow.Test;

/// <summary>
/// Contains tests for ParallelExtensions.ProcessInParallelAsync method.
/// </summary>
public sealed class ParallelExtensionsTest
{
    private readonly Mock<WorkflowContext> _workflowContextMock = new();

    [Fact]
    public async Task ProcessInParallelAsync_WithEmptyInputs_ShouldReturnEmptyArray()
    {
        // Arrange
        var inputs = Array.Empty<int>();

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input => await Task.FromResult(input * 2));

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        WorkflowContext nullContext = null!;
        var inputs = new[] { 1, 2, 3 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await nullContext.ProcessInParallelAsync(
                inputs,
                async input => await Task.FromResult(input * 2)));
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<int> nullInputs = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _workflowContextMock.Object.ProcessInParallelAsync(
                nullInputs,
                async input => await Task.FromResult(input * 2)));
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithNullTaskFactory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var inputs = new[] { 1, 2, 3 };
        Func<int, Task<int>> nullTaskFactory = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _workflowContextMock.Object.ProcessInParallelAsync(
                inputs,
                nullTaskFactory));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task ProcessInParallelAsync_WithInvalidMaxConcurrency_ShouldThrowArgumentOutOfRangeException(int maxConcurrency)
    {
        // Arrange
        var inputs = new[] { 1, 2, 3 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _workflowContextMock.Object.ProcessInParallelAsync(
                inputs,
                async input => await Task.FromResult(input * 2),
                maxConcurrency));
    }
    
    [Fact]
    public async Task ProcessInParallelAsync_ShouldPreserveInputOrder_WhenTasksCompleteOutOfOrder()
    {
        var context = new FakeWorkflowContext();

        var inputs = new[] { 1, 2, 3, 4 };

        var results = await context.ProcessInParallelAsync(
            inputs,
            async i =>
            {
                await Task.Delay(i == 1 ? 50 : 1);
                return i * 10;
            },
            maxConcurrency: 4);

        Assert.Equal(new[] { 10, 20, 30, 40 }, results);
    }
    
    [Fact]
    public async Task ProcessInParallelAsync_ShouldEnumerateInputsOnlyOnce()
    {
        var context = new FakeWorkflowContext();
        var trackingInputs = new SingleEnumerationEnumerable<int>(Enumerable.Range(1, 10));

        var results = await context.ProcessInParallelAsync(
            trackingInputs,
            i => Task.FromResult(i * 3),
            maxConcurrency: 3);

        Assert.Equal(1, trackingInputs.EnumerationCount);
        Assert.Equal(Enumerable.Range(1, 10).Select(i => i * 3).ToArray(), results);
    }

    [Fact]
    public async Task ProcessInParallelAsync_ShouldRespectMaxConcurrency()
    {
        // Arrange
        var context = new FakeWorkflowContext();
        var inputs = Enumerable.Range(0, 20).ToArray();
        const int maxConcurrency = 5;
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;
        var lockObj = new object();

        // Act
        await context.ProcessInParallelAsync(
                inputs,
                async _ =>
                {
                    lock (lockObj)
                    {
                        currentConcurrency++;
                        if (currentConcurrency > maxObservedConcurrency)
                        {
                            maxObservedConcurrency = currentConcurrency;
                        }
                    }

                    // Simulate work that takes time to allow concurrency to build up
                    await Task.Delay(10);

                    lock (lockObj)
                    {
                        currentConcurrency--;
                    }

                    return 0;
                },
                maxConcurrency);

        // Assert
        Assert.True(maxObservedConcurrency <= maxConcurrency, 
                $"Max concurrency observed ({maxObservedConcurrency}) exceeded limit ({maxConcurrency})");
        Assert.True(maxObservedConcurrency > 1, "Expected parallelism did not occur");
    }

    [Fact]
    public async Task ProcessInParallelAsync_ShouldAggregateExceptions_WhenTasksFail()
    {
        // Arrange
        var context = new FakeWorkflowContext();
        var inputs = new[] { 1, 2, 3, 4, 5 };
        var expectedMessage = "Test exception";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
                await context.ProcessInParallelAsync(
                    inputs,
                    async i =>
                    {
                        if (i % 2 == 0) // Fail on even numbers
                        {
                            await Task.Yield();
                            throw new InvalidOperationException($"{expectedMessage} {i}");
                        }
                        return i;
                    },
                    maxConcurrency: 2));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.All(ex.InnerExceptions, e => Assert.IsType<InvalidOperationException>(e));
    }

    [Fact]
    public async Task ProcessInParallelAsync_ShouldAggregateExceptions_WhenFactoryThrowsSynchronously()
    {
        // Arrange
        var context = new FakeWorkflowContext();
        var inputs = new[] { 1, 2, 3 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
                await context.ProcessInParallelAsync<int, int>(
                    inputs,
                    i =>
                    {
                        if (i == 2)
                        {
                            throw new InvalidOperationException("Sync factory failure");
                        }
                        return Task.FromResult(i);
                    },
                    maxConcurrency: 2));

        Assert.Single(ex.InnerExceptions);
        Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
        Assert.Equal("Sync factory failure", ex.InnerExceptions[0].Message);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithInputCountGreaterThanMaxConcurrency_ShouldProcessAll()
    {
        // Arrange
        var context = new FakeWorkflowContext();
        var count = 10;
        var inputs = Enumerable.Range(0, count).ToArray();
        var processedCount = 0;

        // Act
        var results = await context.ProcessInParallelAsync(
                inputs,
                async i =>
                {
                    await Task.Yield();
                    Interlocked.Increment(ref processedCount);
                    return i;
                },
                maxConcurrency: 2); // Significantly smaller than input count

        // Assert
        Assert.Equal(count, results.Length);
        Assert.Equal(count, processedCount);
        Assert.Equal(inputs, results);
    }

    private class TestInput
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private class TestOutput
    {
        public int ProcessedId { get; set; }
        public string ProcessedValue { get; set; } = string.Empty;
    }
    
    private sealed class FakeWorkflowContext : WorkflowContext
    {
        public override string Name => "wf";
        public override string InstanceId => "i";
        public override DateTime CurrentUtcDateTime => DateTime.UtcNow;
        public override bool IsReplaying => false;
        public override bool IsPatched(string patchName) => true;

        public override Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null) => throw new NotSupportedException();
        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout) => throw new NotSupportedException();
        public override void SendEvent(string instanceId, string eventName, object payload) => throw new NotSupportedException();
        public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException();
        public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null) => throw new NotSupportedException();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) => throw new NotSupportedException();
        public override Guid NewGuid() => Guid.NewGuid();
        public override ILogger CreateReplaySafeLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public override ILogger CreateReplaySafeLogger(Type type) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public override ILogger CreateReplaySafeLogger<T>() => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }
    
    private sealed class SingleEnumerationEnumerable<T>(IEnumerable<T> inner) : IEnumerable<T>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
