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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public sealed class BulkPublishEventExample : Example
{
    private const string PubsubName = "pubsub";
    private const string TopicName = "deposit";
        
    IReadOnlyList<object> BulkPublishData = new List<object>() {
        new { Id = "17", Amount = 10m },
        new { Id = "18", Amount = 20m },
        new { Id = "19", Amount = 30m }
    };

    public override string DisplayName => "Bulk Publishing Events";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        var res = await client.BulkPublishEventAsync(PubsubName, TopicName, 
            BulkPublishData, cancellationToken: cancellationToken);

        if (res != null) {
            if (res.FailedEntries.Count > 0)
            {
                Console.WriteLine("Some events failed to be published!");
                    
                foreach (var failedEntry in res.FailedEntries)
                {
                    Console.WriteLine("EntryId : " + failedEntry.Entry.EntryId + " Error message : " + 
                                      failedEntry.ErrorMessage);
                }
            }
            else
            {
                Console.WriteLine("Published multiple deposit events!");    
            }
        } else {
            throw new Exception("null response from dapr");
        }
    }
}
