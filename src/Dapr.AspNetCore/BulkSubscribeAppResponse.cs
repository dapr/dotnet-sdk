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

using System.Collections.Generic;

namespace Dapr.AspNetCore;

/// <summary>
/// Response from the application containing status for each entry in the bulk message.
/// It is posted to the bulk subscribe handler.
/// </summary>
public class BulkSubscribeAppResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeAppResponse"/> class.
    /// </summary>
    /// <param name="statuses">List of statuses.</param>
    public BulkSubscribeAppResponse(List<BulkSubscribeAppResponseEntry> statuses)
    {
        this.Statuses = statuses;
    }
        
    /// <summary>
    /// List of statuses.
    /// </summary>
    public List<BulkSubscribeAppResponseEntry> Statuses { get; }
}
