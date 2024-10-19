using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Data;
using Dapr.Common.Data.Operations;
using Xunit;

namespace Dapr.Common.Test.Data;

public class DataPipelineTests
{
    [Fact]
    public async Task ProcessAsync_ShouldProcessOperationsInOrder()
    {
        // Arrange
        var operations = new List<object>
        {
            new MockOperation1(),
            new MockOperation2()
        };
        var pipeline = new DataPipeline(operations);

        // Act
        var result = await pipeline.ProcessAsync<string, string>("input");

        // Assert
        Assert.Equal("input-processed1-processed2", result.Payload);
        Assert.Contains("Operation1", result.Metadata);
        Assert.Contains("Operation2", result.Metadata);
    }

    [Fact]
    public async Task ReverseAsync_ShouldReverseOperationsInOrder()
    {
        // Arrange
        var operations = new List<object>
        {
            new MockOperation1(),
            new MockOperation2()
        };
        var pipeline = new DataPipeline(operations);

        // Act
        var result = await pipeline.ReverseProcessAsync<string, string>("input-processed1-processed2");

        // Assert
        Assert.Equal("input", result.Payload);
        Assert.Contains("Operation1", result.Metadata);
        Assert.Contains("Operation2", result.Metadata);
    }

    private class MockOperation1 : IDaprDataOperation<string, string>
    {
        public Task<DaprDataOperationPayload<string>> ExecuteAsync(string input, CancellationToken cancellationToken)
        {
            var result = new DaprDataOperationPayload<string>($"{input}-processed1")
            {
                Metadata = new Dictionary<string, string> { { "Operation1", "Processed" } }
            };
            return Task.FromResult(result);
        }

        /// <summary>
        /// Reverses the data operation.
        /// </summary>
        /// <param name="input">The processed input data being reversed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reversed output data and metadata for the operation.</returns>
        public Task<DaprDataOperationPayload<string>> ReverseAsync(DaprDataOperationPayload<string> input, CancellationToken cancellationToken)
        {
            var result = new DaprDataOperationPayload<string>(input.Payload.Replace("-processed1", ""))
            {
                Metadata = new Dictionary<string, string> { { "Operation1", "Reversed" } }
            };
            return Task.FromResult(result);
        }

        /// <summary>
        /// The name of the operation.
        /// </summary>
        public string Name => "Test";
    }

    private class MockOperation2 : IDaprDataOperation<string, string>
    {
        /// <summary>
        /// The name of the operation.
        /// </summary>
        public string Name => "Test";

        /// <summary>
        /// Executes the data processing operation. 
        /// </summary>
        /// <param name="input">The input data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The output data and metadata for the operation.</returns>
        public Task<DaprDataOperationPayload<string>> ExecuteAsync(string input, CancellationToken cancellationToken = default)
        {
            var result = new DaprDataOperationPayload<string>($"{input}-processed2")
            {
                Metadata = new Dictionary<string, string> { { "Operation2", "Processed" } }
            };
            return Task.FromResult(result);
        }

        /// <summary>
        /// Reverses the data operation.
        /// </summary>
        /// <param name="input">The processed input data being reversed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reversed output data and metadata for the operation.</returns>
        public Task<DaprDataOperationPayload<string>> ReverseAsync(DaprDataOperationPayload<string> input, CancellationToken cancellationToken)
        {
            var result = new DaprDataOperationPayload<string>(input.Payload.Replace("-processed2", ""))
            {
                Metadata = new Dictionary<string, string> { { "Operation2", "Reversed" } }
            };
            return Task.FromResult(result);
        }
    }
}
