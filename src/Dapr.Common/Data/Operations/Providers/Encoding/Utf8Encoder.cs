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

namespace Dapr.Common.Data.Operations.Providers.Encoding;

/// <summary>
/// Responsible for encoding a string to a byte array.
/// </summary>
public sealed class Utf8Encoder : IDaprDataEncoder
{
    /// <summary>
    /// The name of the operation.
    /// </summary>
    public string Name => "Dapr.Encoding.Utf8";

    /// <summary>
    /// Executes the data processing operation. 
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output data and metadata for the operation.</returns>
    public Task<DaprOperationPayload<ReadOnlyMemory<byte>>> ExecuteAsync(string? input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));
        
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var result = new DaprOperationPayload<ReadOnlyMemory<byte>>(bytes);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Reverses the data operation.
    /// </summary>
    /// <param name="input">The processed input data being reversed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reversed output data and metadata for the operation.</returns>
    public Task<DaprOperationPayload<string?>> ReverseAsync(DaprOperationPayload<ReadOnlyMemory<byte>> input, CancellationToken cancellationToken = default)
    {
        var strValue = System.Text.Encoding.UTF8.GetString(input.Payload.Span);
        var result = new DaprOperationPayload<string?>(strValue);
        return Task.FromResult(result);
    }
}
