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
using System.Collections.Generic;
using Xunit;

namespace Dapr.Actors.Extensions;

public sealed class StringExtensionsTests
{
    [Fact]
    public void ValidateMatchesValue()
    {
        var matchingValues = new List<string> { "apples", "bananas", "cherries", };
        const string value = "I have four cherries";

        var result = value.EndsWithAny(matchingValues, StringComparison.InvariantCulture);
        Assert.True(result);
    }
    
    [Fact]
    public void ValidateDoesNotMatchValue()
    {
        var matchingValues = new List<string> { "apples", "bananas", "cherries", };
        const string value = "I have four grapes";

        var result = value.EndsWithAny(matchingValues, StringComparison.InvariantCulture);
        Assert.False(result);
    }
}
