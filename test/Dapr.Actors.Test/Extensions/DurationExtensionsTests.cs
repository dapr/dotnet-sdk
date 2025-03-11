// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System;
using Xunit;

namespace Dapr.Actors.Extensions;

public sealed class DurationExtensionsTests
{
    [Theory]
    [InlineData("@yearly", 364, 0, 0, 0, 0)]
    [InlineData("@monthly", 28, 0, 0, 0, 0 )]
    [InlineData("@weekly", 7, 0, 0, 0, 0 )]
    [InlineData("@daily", 1, 0, 0, 0, 0)]
    [InlineData("@midnight", 0, 0, 0, 0, 0 )]
    [InlineData("@hourly", 0, 1, 0, 0, 0)]
    [InlineData("@every 1h", 0, 1, 0, 0, 0)]
    [InlineData("@every 30m", 0, 0, 30, 0, 0)]
    [InlineData("@every 45s", 0, 0, 0, 45, 0)]
    [InlineData("@every 1.5h", 0, 1, 30, 0, 0)]
    [InlineData("@every 1h30m", 0, 1, 30, 0, 0)]
    [InlineData("@every 1h30m45s", 0, 1, 30, 45, 0)]
    [InlineData("@every 1h30m45.3s", 0, 1, 30, 45, 300)]
    [InlineData("@every 100ms", 0, 0, 0, 0, 100)]
    [InlineData("@every 1s500ms", 0, 0, 0, 1, 500)]
    [InlineData("@every 1m1s", 0, 0, 1, 1, 0)]
    [InlineData("@every 1.1m", 0, 0, 1, 6, 0)]
    [InlineData("@every 1.5h30m45s100ms", 0, 2, 0, 45, 100)]
    public void ValidatePrefixedPeriodParsing(string input, int expectedDays, int expectedHours, int expectedMinutes, int expectedSeconds, int expectedMilliseconds)
    {
        var result = input.FromPrefixedPeriod();

        if (input is "@yearly" or "@monthly")
        {
            Assert.True(result.Days >= expectedDays);
            return;
        }
        
        Assert.Equal(expectedDays, result.Days);
        Assert.Equal(expectedHours, result.Hours);
        Assert.Equal(expectedMinutes, result.Minutes);
        Assert.Equal(expectedSeconds, result.Seconds);
        Assert.Equal(expectedMilliseconds, result.Milliseconds);
    }

    [Theory]
    [InlineData("@yearly", true)]
    [InlineData("@monthly", true)]
    [InlineData("@weekly", true)]
    [InlineData("@daily", true)]
    [InlineData("@midnight", true)]
    [InlineData("@hourly", true)]
    [InlineData("@every 1h", true)]
    [InlineData("@every 30m", true)]
    [InlineData("@every 45s", true)]
    [InlineData("@every 1.5h", true)]
    [InlineData("@every 1h30m", true)]
    [InlineData("@every 1h30m45s", true)]
    [InlineData("@every 1h30m45.3s", true)]
    [InlineData("@every 100ms", true)]
    [InlineData("@every 1s500ms", true)]
    [InlineData("@every 1m1s", true)]
    [InlineData("@every 1.1m", true)]
    [InlineData("@every 1.5h30m45s100ms", true)]
    public void TestIsDurationExpression(string input, bool expectedResult)
    {
        var actualResult = input.IsDurationExpression();
        Assert.Equal(expectedResult, actualResult);
    }

    [Fact]
    public void ValidateExceptionForUnknownExpression()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var result = "every 100s".FromPrefixedPeriod();
        });
    }
}
