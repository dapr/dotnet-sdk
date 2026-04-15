// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Shouldly;
using Xunit;

namespace Dapr.VirtualActors.Abstractions.Test;

public class ConditionalValueTests
{
    [Fact]
    public void None_HasNoValue()
    {
        var result = ConditionalValue<int>.None;
        result.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void Some_HasValue()
    {
        var result = ConditionalValue<string>.Some("hello");
        result.HasValue.ShouldBeTrue();
        result.Value.ShouldBe("hello");
    }

    [Fact]
    public void Constructor_WithTrue_HasValue()
    {
        var result = new ConditionalValue<int>(true, 42);
        result.HasValue.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithFalse_HasNoValue()
    {
        var result = new ConditionalValue<int>(false, default);
        result.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = ConditionalValue<int>.Some(42);
        var b = ConditionalValue<int>.Some(42);
        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = ConditionalValue<int>.Some(42);
        var b = ConditionalValue<int>.Some(99);
        a.ShouldNotBe(b);
    }
}
