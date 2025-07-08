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

using Moq;

namespace Dapr.Workflow.Test;

/// <summary>
/// Contains tests for ParallelExtensions.ProcessInParallelAsync method.
/// </summary>
public class ParallelExtensionsTest
{
    private readonly Mock<WorkflowContext> _workflowContextMock = new();

    [Fact]
    public async Task ProcessInParallelAsync_WithValidInputs_ShouldProcessAllItemsSuccessfully()
    {
        // Arrange
        var inputs = new[] { 1, 2, 3, 4, 5 };
        var expectedResults = new[] { 2, 4, 6, 8, 10 };

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input => await Task.FromResult(input * 2),
            maxConcurrency: 2);

        // Assert
        Assert.Equal(expectedResults, results);
    }

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
    public async Task ProcessInParallelAsync_WithSingleInput_ShouldProcessCorrectly()
    {
        // Arrange
        var inputs = new[] { 42 };

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input => await Task.FromResult(input.ToString()));

        // Assert
        Assert.Single(results);
        Assert.Equal("42", results[0]);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithMaxConcurrency1_ShouldProcessSequentially()
    {
        // Arrange
        var inputs = new[] { 1, 2, 3 };
        var processedOrder = new List<int>();
        var processingTasks = new List<Task>();

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input =>
            {
                processedOrder.Add(input);
                await Task.Delay(10); // Small delay to ensure order
                return input * 2;
            },
            maxConcurrency: 1);

        // Assert
        Assert.Equal(new[] { 2, 4, 6 }, results);
        Assert.Equal(new[] { 1, 2, 3 }, processedOrder);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithHighConcurrency_ShouldRespectConcurrencyLimit()
    {
        // Arrange
        var inputs = Enumerable.Range(1, 100).ToArray();
        var concurrentTasks = 0;
        var maxConcurrentTasks = 0;
        var lockObj = new object();

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input =>
            {
                lock (lockObj)
                {
                    concurrentTasks++;
                    maxConcurrentTasks = Math.Max(maxConcurrentTasks, concurrentTasks);
                }

                await Task.Delay(10);

                lock (lockObj)
                {
                    concurrentTasks--;
                }

                return input * 2;
            },
            maxConcurrency: 10);

        // Assert
        Assert.Equal(inputs.Length, results.Length);
        Assert.True(maxConcurrentTasks <= 10, $"Expected max concurrent tasks <= 10, but was {maxConcurrentTasks}");
        Assert.True(maxConcurrentTasks >= 1, "At least one task should have been executed");
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
    public async Task ProcessInParallelAsync_WithTaskFailure_ShouldThrowAggregateException()
    {
        // Arrange
        var inputs = new[] { 1, 2, 3, 4, 5 };
        //var expectedSuccessfulResults = 3; // Items 1, 3, 5 should succeed
        const int expectedFailures = 2; // Items 2, 4 should fail

        // Act & Assert
        var aggregateException = await Assert.ThrowsAsync<AggregateException>(
            async () => await _workflowContextMock.Object.ProcessInParallelAsync(
                inputs,
                async input =>
                {
                    await Task.Delay(10);
                    if (input % 2 == 0) // Even numbers fail
                        throw new InvalidOperationException($"Failed processing item {input}");
                    return input * 2;
                },
                maxConcurrency: 2));

        // Assert
        Assert.Equal(expectedFailures, aggregateException.InnerExceptions.Count);
        Assert.All(aggregateException.InnerExceptions, ex => 
            Assert.IsType<InvalidOperationException>(ex));
        Assert.Contains("2 out of 5 tasks failed", aggregateException.Message);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithAllTasksFailure_ShouldThrowAggregateExceptionWithAllFailures()
    {
        // Arrange
        var inputs = new[] { 1, 2, 3 };
        var expectedMessage = "Test failure";

        // Act & Assert
        var aggregateException = await Assert.ThrowsAsync<AggregateException>(
            async () => await _workflowContextMock.Object.ProcessInParallelAsync<int, object>(
                inputs,
                async input =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException($"{expectedMessage} {input}");
                }));

        // Assert
        Assert.Equal(3, aggregateException.InnerExceptions.Count);
        Assert.All(aggregateException.InnerExceptions, ex => 
        {
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains(expectedMessage, ex.Message);
        });
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithMixedSuccessAndFailure_ShouldPreserveOrderInResults()
    {
        // Arrange
        var inputs = new[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        var aggregateException = await Assert.ThrowsAsync<AggregateException>(
            async () => await _workflowContextMock.Object.ProcessInParallelAsync(
                inputs,
                async input =>
                {
                    await Task.Delay(input * 10); // Different delays to test ordering
                    if (input == 3)
                        throw new InvalidOperationException($"Failed on item {input}");
                    return input * 2;
                },
                maxConcurrency: 2));

        // Assert that failure occurred
        Assert.Single(aggregateException.InnerExceptions);
        Assert.Contains("Failed on item 3", aggregateException.InnerExceptions[0].Message);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithDefaultMaxConcurrency_ShouldUseDefaultValue()
    {
        // Arrange
        var inputs = Enumerable.Range(1, 20).ToArray();
        var concurrentTasks = 0;
        var maxConcurrentTasks = 0;
        var lockObj = new object();

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input =>
            {
                lock (lockObj)
                {
                    concurrentTasks++;
                    maxConcurrentTasks = Math.Max(maxConcurrentTasks, concurrentTasks);
                }

                await Task.Delay(50); // Longer delay to ensure concurrency

                lock (lockObj)
                {
                    concurrentTasks--;
                }

                return input * 2;
            }); // Using default maxConcurrency (should be 5)

        // Assert
        Assert.Equal(inputs.Length, results.Length);
        Assert.True(maxConcurrentTasks <= 5, $"Expected max concurrent tasks <= 5, but was {maxConcurrentTasks}");
        Assert.True(maxConcurrentTasks >= 1, "At least one task should have been executed");
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithDifferentInputAndOutputTypes_ShouldHandleTypeConversion()
    {
        // Arrange
        var inputs = new[] { "1", "2", "3", "4", "5" };
        var expectedResults = new[] { 1, 2, 3, 4, 5 };

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input => await Task.FromResult(int.Parse(input)),
            maxConcurrency: 3);

        // Assert
        Assert.Equal(expectedResults, results);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithComplexObjects_ShouldProcessCorrectly()
    {
        // Arrange
        var inputs = new[]
        {
            new TestInput { Id = 1, Value = "Test1" },
            new TestInput { Id = 2, Value = "Test2" },
            new TestInput { Id = 3, Value = "Test3" }
        };

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input => await Task.FromResult(new TestOutput 
            { 
                ProcessedId = input.Id * 10, 
                ProcessedValue = input.Value.ToUpper() 
            }),
            maxConcurrency: 2);

        // Assert
        Assert.Equal(3, results.Length);
        Assert.Equal(10, results[0].ProcessedId);
        Assert.Equal("TEST1", results[0].ProcessedValue);
        Assert.Equal(20, results[1].ProcessedId);
        Assert.Equal("TEST2", results[1].ProcessedValue);
        Assert.Equal(30, results[2].ProcessedId);
        Assert.Equal("TEST3", results[2].ProcessedValue);
    }

    [Fact]
    public async Task ProcessInParallelAsync_WithLargeDataset_ShouldHandleEfficiently()
    {
        // Arrange
        var inputs = Enumerable.Range(1, 1000).ToArray();
        var expectedResults = inputs.Select(x => x * 2).ToArray();

        // Act
        var results = await _workflowContextMock.Object.ProcessInParallelAsync(
            inputs,
            async input => await Task.FromResult(input * 2),
            maxConcurrency: 10);

        // Assert
        Assert.Equal(expectedResults, results);
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
}
