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

public class VirtualActorIdTests
{
    [Fact]
    public void Constructor_WithValidId_Succeeds()
    {
        var id = new VirtualActorId("test-123");
        id.GetId().ShouldBe("test-123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidId_Throws(string? invalidId)
    {
        Should.Throw<ArgumentException>(() => new VirtualActorId(invalidId!));
    }

    [Fact]
    public void Equality_SameId_AreEqual()
    {
        var id1 = new VirtualActorId("abc");
        var id2 = new VirtualActorId("abc");

        (id1 == id2).ShouldBeTrue();
        id1.Equals(id2).ShouldBeTrue();
        id1.GetHashCode().ShouldBe(id2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var id1 = new VirtualActorId("abc");
        var id2 = new VirtualActorId("def");

        (id1 != id2).ShouldBeTrue();
        id1.Equals(id2).ShouldBeFalse();
    }

    [Fact]
    public void Equality_IsCaseSensitive()
    {
        var lower = new VirtualActorId("abc");
        var upper = new VirtualActorId("ABC");

        (lower == upper).ShouldBeFalse();
    }

    [Fact]
    public void CompareTo_OrdersAlphabetically()
    {
        var a = new VirtualActorId("alpha");
        var b = new VirtualActorId("beta");

        (a < b).ShouldBeTrue();
        (b > a).ShouldBeTrue();
        (a <= b).ShouldBeTrue();
        (b >= a).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ReturnsId()
    {
        var id = new VirtualActorId("my-actor");
        id.ToString().ShouldBe("my-actor");
    }

    [Fact]
    public void Equals_WithObject_WorksCorrectly()
    {
        var id = new VirtualActorId("test");

        id.Equals((object)new VirtualActorId("test")).ShouldBeTrue();
        id.Equals((object)new VirtualActorId("other")).ShouldBeFalse();
        id.Equals((object)"test").ShouldBeFalse();
        id.Equals(null).ShouldBeFalse();
    }
}
