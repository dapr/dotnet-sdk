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
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public class PublishBytesExample : Example
{
    public override string DisplayName => "Publish Bytes";

    public async override Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        var transaction = new { Id = "17", Amount = 30m };
        var content = JsonSerializer.SerializeToUtf8Bytes(transaction);

        await client.PublishByteEventAsync(pubsubName, "deposit", content.AsMemory(), MediaTypeNames.Application.Json, new Dictionary<string, string> { }, cancellationToken);
        Console.WriteLine("Published deposit event!");
    }
}