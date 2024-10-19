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

using System.Text.Json;

namespace Dapr.Common.Data.Operations.Providers.Serialization;

/// <summary>
/// Provides serialization capabilities using System.Text.Json.
/// </summary>
public class DaprSystemTextJsonSerializer<T> : IDaprDataSerializer<T>
{
    /// <summary>
    /// Optionally provided <see cref="JsonSerializerOptions"/>.
    /// </summary>
    private JsonSerializerOptions? options = new (JsonSerializerDefaults.Web);
    
    /// <summary>
    /// The name of the operation.
    /// </summary>
    public string Name => "Dapr.Serialization.SystemTextJsonSerializer";

    /// <summary>
    /// Executes the data processing operation. 
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output data and metadata for the operation.</returns>
    public Task<DaprDataOperationPayload<string>> ExecuteAsync(T input, CancellationToken cancellationToken = default)
    {
        var jsonResult = JsonSerializer.Serialize(input, options);
        var result = new DaprDataOperationPayload<string>(jsonResult, Name);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Reverses the data operation.
    /// </summary>
    /// <param name="input">The processed input data being reversed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reversed output data and metadata for the operation.</returns>
    public Task<DaprDataOperationPayload<T?>> ReverseAsync(DaprDataOperationPayload<string> input,
        CancellationToken cancellationToken = default)
    {
        var value = JsonSerializer.Deserialize<T>(input.Payload, options);
        var result = new DaprDataOperationPayload<T?>(value);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Used to provide a <see cref="JsonSerializerOptions"/> to the operation.
    /// </summary>
    /// <param name="jsonSerializerOptions">The configuration options to use.</param>
    public void UseOptions(JsonSerializerOptions jsonSerializerOptions)
    {
        this.options = jsonSerializerOptions;
    }
}
