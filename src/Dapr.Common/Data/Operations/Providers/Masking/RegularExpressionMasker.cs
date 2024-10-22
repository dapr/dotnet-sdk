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

using System.Text.RegularExpressions;

namespace Dapr.Common.Data.Operations.Providers.Masking;

/// <summary>
/// Performs a masking operation on the provided input.
/// </summary>
public class RegularExpressionMasker : IDaprDataMasker
{
    private readonly Dictionary<Regex, string> patterns = new();

    /// <summary>
    /// The name of the operation.
    /// </summary>
    public string Name => "Dapr.Masking.Regexp";

    /// <summary>
    /// Executes the data processing operation. 
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output data and metadata for the operation.</returns>
    public Task<DaprOperationPayload<string?>> ExecuteAsync(string? input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
            return Task.FromResult(new DaprOperationPayload<string?>(null));
        
        var updatedValue = input;
        foreach (var pattern in patterns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            updatedValue = pattern.Key.Replace(input, pattern.Value);
        }

        var payloadResult = new DaprOperationPayload<string?>(updatedValue);
        return Task.FromResult(payloadResult);
    }

    /// <summary>
    /// Reverses the data operation.
    /// </summary>
    /// <param name="input">The processed input data being reversed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reversed output data and metadata for the operation.</returns>
    public Task<DaprOperationPayload<string?>> ReverseAsync(DaprOperationPayload<string?> input,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DaprOperationPayload<string?>(input.Payload));

    /// <summary>
    /// Registers a pattern to match against.
    /// </summary>
    /// <param name="pattern">The regular expression to match to.</param>
    /// <param name="replacement">The string to place the matching value with.</param>
    public void RegisterMatch(Regex pattern, string replacement)
    {
        patterns.Add(pattern, replacement);
    }
}
