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
using Dapr.Actors.Runtime;
using Xunit;

namespace Dapr.Actors;

public class ConverterUtilsTests
{
    [Fact]
    public void Deserialize_Period_Duration1()
    {
        var result = ConverterUtils.ConvertTimeSpanValueFromISO8601Format("@every 15m");
        Assert.Equal(TimeSpan.FromMinutes(15), result.Item1);
    }

    [Fact]
    public void Deserialize_Period_Duration2()
    {
        var result = ConverterUtils.ConvertTimeSpanValueFromISO8601Format("@hourly");
        Assert.Equal(TimeSpan.FromHours(1), result.Item1);
    }
}
