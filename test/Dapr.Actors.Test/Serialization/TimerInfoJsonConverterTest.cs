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

using System;
using System.Text.Json;
using Xunit;

#pragma warning disable 0618
namespace Dapr.Actors.Runtime;

public class TimerInfoJsonConverterTest
{
    [Theory]
    [InlineData("test", new byte[] {1, 2, 3}, 2, 4)]
    [InlineData(null, new byte[] {1}, 2, 4)]
    [InlineData("test", null, 2, 4)]
    [InlineData("test", new byte[] {1}, 0, 4)]
    [InlineData("test", new byte[] {1}, 2, 0)]
    public void CanSerializeAndDeserializeTimerInfo(string callback, byte[] state, int dueTime, int period)
    {
        var timerInfo = new TimerInfo(callback, state, TimeSpan.FromSeconds(dueTime), TimeSpan.FromSeconds(period));

        // We use strings for ActorId - the result should be the same as passing the Id directly.
        var serializedTimerInfo = JsonSerializer.Serialize<TimerInfo>(timerInfo);

        var deserializedTimerInfo = JsonSerializer.Deserialize<TimerInfo>(serializedTimerInfo);

        Assert.Equal(timerInfo.Callback, deserializedTimerInfo.Callback);
        Assert.Equal(timerInfo.Data, deserializedTimerInfo.Data);
        Assert.Equal(timerInfo.DueTime, deserializedTimerInfo.DueTime);
        Assert.Equal(timerInfo.Period, deserializedTimerInfo.Period);
    }
}
#pragma warning restore 0618
