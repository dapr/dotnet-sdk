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

namespace Dapr.Client;

/// <summary>
/// Class representing the status of each event that was published using BulkPublishRequest.
/// </summary>
/// <param name="entry">The entry that failed to be published.</param>
/// <param name="errorMessage">Error message as to why the entry failed to publish.</param>
public class BulkPublishResponseFailedEntry<TValue>(BulkPublishEntry<TValue> entry, string errorMessage)
{
    /// <summary>
    /// The entry that has failed.
    /// </summary>
    public BulkPublishEntry<TValue> Entry { get; } = entry;

    /// <summary>
    /// Error message as to why the entry failed to publish.
    /// </summary>
    public string ErrorMessage { get; } = errorMessage;
}
