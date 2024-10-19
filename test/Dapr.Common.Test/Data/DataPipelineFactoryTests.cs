using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Data;
using Dapr.Common.Data.Attributes;
using Dapr.Common.Data.Operations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Common.Test.Data;

public class DataPipelineFactoryTests
{
    [Fact]
    public void CreatePipeline_ShouldCreatePipelineWithCorrectOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<MockOperation1>();
        services.AddSingleton<MockOperation2>();
        services.AddSingleton<DataPipelineFactory>();
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataPipelineFactory>();

        // Act
        var pipeline = factory.CreatePipeline<TestDataProcessor>();

        // Assert
        Assert.NotNull(pipeline);
    }

    [DataOperation(typeof(MockOperation1), typeof(MockOperation2))]
    private class TestDataProcessor
    {
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
