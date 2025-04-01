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

namespace Dapr.Client.Test;

using System;
using Xunit;

public class ArgumentVerifierTest
{
    [Fact]
    public void ThrowIfNull_RespectsArgumentName()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            ArgumentVerifier.ThrowIfNull(null, "args");
        });

        Assert.Contains("args", ex.Message);
    }

    [Fact]
    public void ThrowIfNullOrEmpty_RespectsArgumentName_WhenValueIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(null, "args");
        });

        Assert.Contains("args", ex.Message);
    }

    [Fact]
    public void ThrowIfNullOrEmpty_RespectsArgumentName_WhenValueIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(string.Empty, "args");
        });

        Assert.Contains("args", ex.Message);
    }
}