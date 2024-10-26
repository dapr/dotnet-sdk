﻿// ------------------------------------------------------------------------
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

using System;
using System.Text.Json;
using Dapr.Jobs.Models;
using Dapr.Jobs.Models.Responses;

namespace Dapr.Jobs.Test.Responses;

public sealed class DaprJobDetailsTests
{
    [Fact]
    public void ValidatePropertiesAreAsSet()
    {
        var payload = new TestPayload("Dapr", "Red");
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var dueTime = DateTimeOffset.UtcNow.AddDays(2);
        var ttl = DateTimeOffset.UtcNow.AddMonths(3);
        const int repeatCount = 15;

        var details = new DaprJobDetails(DaprJobSchedule.Midnight)
        {
            RepeatCount = repeatCount,
            DueTime = dueTime,
            Payload = payloadBytes,
            Ttl = ttl
        };

        Assert.Equal(repeatCount, details.RepeatCount);
        Assert.Equal(dueTime, details.DueTime);
        Assert.Equal(ttl, details.Ttl);
        Assert.Equal(payloadBytes, details.Payload);
    }

    private sealed record TestPayload(string Name, string Color);
}
