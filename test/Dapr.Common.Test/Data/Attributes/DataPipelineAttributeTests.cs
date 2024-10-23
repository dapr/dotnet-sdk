// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Data.Attributes;
using Dapr.Common.Data.Operations;
using Xunit;

namespace Dapr.Common.Test.Data.Attributes;

public class DataPipelineAttributeTests
{
    [Fact]
    public void DataOperationAttribute_ShouldThrowExceptionForInvalidTypes()
    {
        // Arrange & Act & Assert
        Assert.Throws<DaprException>(() => new DataPipelineAttribute(typeof(InvalidOperation)));
    }

    [Fact]
    public void DataOperationAttribute_ShouldRegisterValidTypes()
    {
        // Arrange & Act
        var attribute = new DataPipelineAttribute(typeof(MockOperation1), typeof(MockOperation2));

        // Assert
        Assert.Equal(2, attribute.DataOperationTypes.Count);
        Assert.Contains(typeof(MockOperation1), attribute.DataOperationTypes);
        Assert.Contains(typeof(MockOperation2), attribute.DataOperationTypes);
    }

    private class InvalidOperation
    {
    }

    private class MockOperation1 : IDaprDataOperation<string, string>
    {
        public Task<DaprOperationPayload<string>> ExecuteAsync(string input, CancellationToken cancellationToken)
        {
            var result = new DaprOperationPayload<string>($"{input}-processed1")
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
        public Task<DaprOperationPayload<string>> ReverseAsync(DaprOperationPayload<string> input,
            CancellationToken cancellationToken)
        {
            var result = new DaprOperationPayload<string>(input.Payload.Replace("-processed1", ""))
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
        public Task<DaprOperationPayload<string>> ExecuteAsync(string input,
            CancellationToken cancellationToken = default)
        {
            var result = new DaprOperationPayload<string>($"{input}-processed2")
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
        public Task<DaprOperationPayload<string>> ReverseAsync(DaprOperationPayload<string> input,
            CancellationToken cancellationToken)
        {
            var result = new DaprOperationPayload<string>(input.Payload.Replace("-processed2", ""))
            {
                Metadata = new Dictionary<string, string> { { "Operation2", "Reversed" } }
            };
            return Task.FromResult(result);
        }
    }
}
