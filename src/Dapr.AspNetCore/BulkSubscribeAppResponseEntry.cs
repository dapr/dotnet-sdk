// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.AspNetCore;

/// <summary>
/// Maps an entry from bulk subscribe messages to a response status.
/// </summary>
public class BulkSubscribeAppResponseEntry
{

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeAppResponseEntry"/> class.
    /// </summary>
    /// <param name="entryId">Entry ID of the event.</param>
    /// <param name="status">Status of the event processing in application.</param>
    public BulkSubscribeAppResponseEntry(string entryId, BulkSubscribeAppResponseStatus status) {
        this.EntryId = entryId;
        this.Status = status.ToString();
    }

    /// <summary>
    /// Entry ID of the event.
    /// </summary>
    public string EntryId { get; }
        
    /// <summary>
    /// Status of the event processing in application.
    /// </summary>
    public string Status { get; }
}
