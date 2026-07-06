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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public class PublishEventExample : Example
{
    public override string DisplayName => "Publishing Events";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        var eventData = new { Id = "17", Amount = 10m, };
        await client.PublishEventAsync(pubsubName, "deposit", eventData, cancellationToken);
        Console.WriteLine("Published deposit event!");
    }
}