// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.Core.Test;

/// <summary>
/// The sidecar reports periods as "@every" plus Go's time.Duration.String() output, echoes due times as they
/// were registered (Go or ISO 8601 durations, or an RFC 3339 timestamp over HTTP) and reports TTLs as
/// RFC 3339 expirations. The parser must accept every one of those shapes.
/// </summary>
public sealed class ActorScheduleDurationParserTests
{
    [Theory]
    [InlineData("60000ms", 60_000)]
    [InlineData("0.5ms", 0.5)]
    [InlineData("1.5s", 1500)]
    [InlineData("1h30m0s", 5_400_000)]
    [InlineData("0s", 0)]
    [InlineData("500µs", 0.5)]
    [InlineData("500μs", 0.5)]
    [InlineData("500us", 0.5)]
    [InlineData("1ms500µs", 1.5)]
    [InlineData("100ns", 0.0001)]
    [InlineData("@every 500µs", 0.5)]
    [InlineData("@every 10s", 10_000)]
    [InlineData("@every 1h30m0s", 5_400_000)]
    [InlineData("PT10S", 10_000)]
    [InlineData("R5/PT10S", 10_000)]
    [InlineData("P1DT1H", 90_000_000)]
    public void Parses_go_and_iso_durations(string value, double expectedMilliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), ActorScheduleDurationParser.Parse(value));
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), ActorScheduleDurationParser.ParseOptional(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Missing_durations_parse_as_null(string? value)
    {
        Assert.Null(ActorScheduleDurationParser.ParseOptional(value));
        Assert.Null(ActorScheduleDurationParser.ParseOptionalTimestamp(value));
        Assert.Equal((null, null), ActorScheduleDurationParser.ParseOptionalDueTime(value));
    }

    [Theory]
    [InlineData("2026-10-21T10:15:30Z", 0)]
    [InlineData("2026-10-21T10:15:30.5Z", 5_000_000)]
    [InlineData("2026-10-21T10:15:30.123456789Z", 1_234_568)] // Go nanoseconds round to .NET ticks.
    [InlineData("2026-10-21T12:15:30+02:00", 0)]
    [InlineData("2026-10-21T10:15:30.25-00:00", 2_500_000)]
    public void Parses_rfc3339_timestamps_to_the_same_utc_instant(string value, long fractionTicks)
    {
        var expected = new DateTimeOffset(2026, 10, 21, 10, 15, 30, TimeSpan.Zero).AddTicks(fractionTicks);

        var parsed = ActorScheduleDurationParser.ParseOptionalTimestamp(value);

        Assert.NotNull(parsed);
        Assert.Equal(expected.UtcDateTime, parsed.Value.UtcDateTime);
    }

    [Fact]
    public void Due_time_keeps_durations_and_timestamps_apart()
    {
        Assert.Equal((TimeSpan.FromSeconds(10), null), ActorScheduleDurationParser.ParseOptionalDueTime("10s"));
        Assert.Equal((TimeSpan.FromSeconds(10), null), ActorScheduleDurationParser.ParseOptionalDueTime("PT10S"));
        Assert.Equal((TimeSpan.FromMilliseconds(0.5), null), ActorScheduleDurationParser.ParseOptionalDueTime("500µs"));
        Assert.Equal(
            (null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)),
            ActorScheduleDurationParser.ParseOptionalDueTime("2026-10-21T10:00:00Z"));
        Assert.Equal(
            (null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)),
            ActorScheduleDurationParser.ParseOptionalDueTime("2026-10-21T12:00:00+02:00"));
    }

    [Theory]
    [InlineData("5 parsecs")]
    [InlineData("10x")]
    [InlineData("@every")]
    public void Rejects_unknown_formats(string value)
    {
        Assert.Throws<FormatException>(() => ActorScheduleDurationParser.Parse(value));
    }
}
