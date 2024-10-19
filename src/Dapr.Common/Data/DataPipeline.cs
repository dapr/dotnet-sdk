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

using Dapr.Common.Data.Operations;

namespace Dapr.Common.Data;

/// <summary>
/// Processes the data using the provided <see cref="IDaprDataOperation{TInput,TOutput}"/> providers.
/// </summary>
public class DataPipeline
{
    private readonly List<object> operations;

    /// <summary>
    /// Used to initialize a new <see cref="DataPipeline"/>.
    /// </summary>
    public DataPipeline(IEnumerable<object> operations)
    {
        this.operations = operations.ToList();
    }

    /// <summary>
    /// Processes the data in the order of the provided list of <see cref="IDaprDataOperation{TInput,TOutput}"/>.
    /// </summary>
    /// <param name="input">The data to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluated data.</returns>
    public async Task<DaprDataOperationPayload<TOutput?>> ProcessAsync<TInput, TOutput>(TInput input, CancellationToken cancellationToken = default)
    {
        object? currentData = input;
        var combinedMetadata = new Dictionary<string, string>();
        const string operationNameKey = "Ops";
        
        foreach (var operation in operations)
        {
            var method = operation.GetType().GetMethod("ExecuteAsync");
            if (method is null || currentData is null)
                continue;

            var task = (Task)method.Invoke(operation, new object[] { currentData, cancellationToken })!;
            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            var result = (DaprDataOperationPayload<object>)resultProperty?.GetValue(task)!;
            currentData = result.Payload;
            
            foreach (var kvp in result.Metadata)
            {
                //Append the operation name if given that key
                if (kvp.Key == operationNameKey)
                {
                    if (combinedMetadata.TryGetValue(operationNameKey, out var operationName))
                    {
                        combinedMetadata[operationNameKey] = operationName + $",{kvp.Value}";
                    }
                    else
                    {
                        combinedMetadata[operationNameKey] = kvp.Value;
                    }
                }

                combinedMetadata[kvp.Key] = kvp.Value;
            }
        }

        return new DaprDataOperationPayload<TOutput?>((TOutput?)currentData) { Metadata = combinedMetadata };
    }

    /// <summary>
    /// Processes the reverse of the data in the order of the provided list of <see cref="IDaprDataOperation{TInput,TOutput}"/>.
    /// </summary>
    /// <param name="input">The data to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluated data.</returns>
    public async Task<DaprDataOperationPayload<TInput?>> ReverseProcessAsync<TInput, TOutput>(TOutput input, CancellationToken cancellationToken = default)
    {
        object? currentData = input;
        var combinedMetadata = new Dictionary<string, string>();

        for (int i = operations.Count - 1; i >= 0; i--)
        {
            var method = operations[i].GetType().GetMethod("ReverseAsync");
            if (method is null || currentData is null)
                continue;
            
            var result = await (Task<DaprDataOperationPayload<object>>)method.Invoke(operations[i], new[] { currentData, cancellationToken })!;
            currentData = result.Payload;
            foreach (var kvp in result.Metadata)
            {
                combinedMetadata[kvp.Key] = kvp.Value;
            }
        }

        return new DaprDataOperationPayload<TInput?>((TInput?)currentData) { Metadata = combinedMetadata };
    }
}
