// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

namespace Dapr.Client;

/// <summary>
/// Abstraction around a configuration source.
/// </summary>
public abstract class ConfigurationSource : IAsyncEnumerable<IDictionary<string, ConfigurationItem>>
{
    /// <summary>
    /// The Id associated with this configuration source.
    /// </summary>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract IAsyncEnumerator<IDictionary<string, ConfigurationItem>> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}