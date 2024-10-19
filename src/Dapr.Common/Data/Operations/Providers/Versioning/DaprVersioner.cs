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

namespace Dapr.Common.Data.Operations.Providers.Versioning;

/// <summary>
/// Default implementation of an <see cref="IDaprDataVersioner"/> for handling data versioning operations.
/// </summary>
public class DaprVersioner : IDaprDataVersioner
{
    private readonly Dictionary<int, Func<string, string?>> _upgraders = new();
    
    /// <summary>
    /// The name of the operation.
    /// </summary>
    public string Name => "Dapr.Versioning.DaprVersioner";

    /// <summary>
    /// Executes the data processing operation. 
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output data and metadata for the operation.</returns>
    public Task<DaprDataOperationPayload<string>> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reverses the data operation.
    /// </summary>
    /// <param name="input">The processed input data being reversed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reversed output data and metadata for the operation.</returns>
    public Task<DaprDataOperationPayload<string?>> ReverseAsync(DaprDataOperationPayload<string> input, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The current version of the data.
    /// </summary>
    public int CurrentVersion { get; }


    /// <summary>
    /// Registers an upgrade function for a specific version.
    /// </summary>
    /// <param name="fromVersion">The version to upgrade from.</param>
    /// <param name="upgradeFunc">The function to upgrade the data.</param>
    /// <typeparam name="T">The type of data to upgrade.</typeparam>
    public void RegisterUpgrade<T>(int fromVersion, Func<string, string> upgradeFunc)
    {
        _upgraders[fromVersion] = upgradeFunc;
    }

    private record VersionedData<T>(int Version, T Data);
}
