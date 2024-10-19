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

namespace Dapr.Common.Data.Operations;

/// <summary>
/// Represents a data operation.
/// </summary>
public interface IDaprDataOperation
{
    /// <summary>
    /// The name of the operation.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Represents a data operation with generic input and output types.
/// </summary>
/// <typeparam name="TInput">The type of the input data.</typeparam>
/// <typeparam name="TOutput">The type of the output data.</typeparam>
public interface IDaprDataOperation<TInput, TOutput> : IDaprDataOperation
{
    /// <summary>
    /// Executes the data processing operation. 
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output data and metadata for the operation.</returns>
    Task<DaprDataOperationPayload<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses the data operation.
    /// </summary>
    /// <param name="input">The processed input data being reversed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reversed output data and metadata for the operation.</returns>
    Task<DaprDataOperationPayload<TInput?>> ReverseAsync(DaprDataOperationPayload<TOutput> input,
        CancellationToken cancellationToken);
}
