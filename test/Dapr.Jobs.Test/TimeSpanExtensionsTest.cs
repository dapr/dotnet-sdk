// ------------------------------------------------------------------------
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
using Xunit;

namespace Dapr.Jobs.Test;

public class TimeSpanExtensionsTest
{
    [Fact]
    public void ToDurationString_ValidateHours()
    {
        var fourHours = TimeSpan.FromHours(4);
        var result = fourHours.ToDurationString();

        Assert.Equal("4h", result);
    }

    [Fact]
    public void ToDurationString_ValidateMinutes()
    {
        var elevenMinutes = TimeSpan.FromMinutes(11);
        var result = elevenMinutes.ToDurationString();

        Assert.Equal("11m", result);
    }

    [Fact]
    public void ToDurationString_ValidateSeconds()
    {
        var fortySeconds = TimeSpan.FromSeconds(40);
        var result = fortySeconds.ToDurationString();

        Assert.Equal("40s", result);
    }

    [Fact]
    public void ToDurationString_ValidateMilliseconds()
    {
        var tenMilliseconds = TimeSpan.FromMilliseconds(10);
        var result = tenMilliseconds.ToDurationString();

        Assert.Equal("10ms", result);
    }

    [Fact]
    public void ToDurationString_HoursAndMinutes()
    {
        var ninetyMinutes = TimeSpan.FromMinutes(90);
        var result = ninetyMinutes.ToDurationString();

        Assert.Equal("1h30m", result);
    }

    [Fact]
    public void ToDurationString_Combined()
    {
        var time = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(24) +
                   TimeSpan.FromMilliseconds(28);
        var result = time.ToDurationString();

        Assert.Equal("2h4m24s28ms", result);
    }

    [Fact]
    public void FromDurationString_AllSegments()
    {
        const string interval = "13h57m4s10ms";
        var result = interval.FromDurationString();

        Assert.Equal(13, result.Hours);
        Assert.Equal(57, result.Minutes);
        Assert.Equal(4, result.Seconds);
        Assert.Equal(10, result.Milliseconds);
    }

    [Fact]
    public void FromDurationString_LimitedSegments1()
    {
        const string interval = "5h12ms";
        var result = interval.FromDurationString();

        Assert.Equal(5, result.Hours);
        Assert.Equal(12, result.Milliseconds);
    }

    [Fact]
    public void FromDurationString_LimitedSegments2()
    {
        const string interval = "5m";
        var result = interval.FromDurationString();

        Assert.Equal(5, result.Minutes);
    }

    [Fact]
    public void FromDurationString_LimitedSegments3()
    {
        const string interval = "16s43ms";
        var result = interval.FromDurationString();

        Assert.Equal(16, result.Seconds);
        Assert.Equal(43, result.Milliseconds);
    }

    [Fact]
    public void FromDurationString_LimitedSegments4()
    {
        const string interval = "4h32m16s";
        var result = interval.FromDurationString();

        Assert.Equal(4, result.Hours);
        Assert.Equal(32, result.Minutes);
        Assert.Equal(16, result.Seconds);
    }
}
