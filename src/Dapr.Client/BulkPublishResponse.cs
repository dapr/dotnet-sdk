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

namespace Dapr.Client;

/// <summary>
/// Class representing the response returned on bulk publishing events.
/// </summary>
/// <param name="failedEntries">The List of BulkPublishResponseEntries representing the list of events that failed to be published.</param>
public class BulkPublishResponse<TValue>(List<BulkPublishResponseFailedEntry<TValue>> failedEntries)
{
    /// <summary>
    /// The List of BulkPublishResponseFailedEntry objects that have failed to publish.
    /// </summary>
    public List<BulkPublishResponseFailedEntry<TValue>> FailedEntries { get; } = failedEntries;
}
