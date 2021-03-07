// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

#pragma warning disable 0618
namespace Dapr.Actors.Runtime
{
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
}
#pragma warning restore 0618
