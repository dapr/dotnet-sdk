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

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class ActorIdTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void Create_StoresValueAndFormatsAsValue()
    {
        var id = ActorId.Create("cart-1");

        Assert.Equal("cart-1", id.Value);
        Assert.Equal("cart-1", id.ToString());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Parse_ReturnsEquivalentActorId()
    {
        Assert.Equal(new ActorId("cart-1"), ActorId.Parse("cart-1"));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Constructor_RejectsInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => new ActorId(""));
        Assert.Throws<ArgumentException>(() => new ActorId(" "));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Constructor_RejectsNull()
    {
        Assert.Throws<ArgumentException>(() => new ActorId(null!));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void TryParse_ReturnsTrueForValidValue()
    {
        var parsed = ActorId.TryParse("actor-7", out var id);

        Assert.True(parsed);
        Assert.Equal("actor-7", id.Value);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void TryParse_ReturnsFalseForInvalidValue()
    {
        Assert.False(ActorId.TryParse(null, out var nullId));
        Assert.Equal(default, nullId);

        Assert.False(ActorId.TryParse("", out var emptyId));
        Assert.Equal(default, emptyId);

        Assert.False(ActorId.TryParse(" ", out var whitespaceId));
        Assert.Equal(default, whitespaceId);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Equality_UsesValue()
    {
        Assert.Equal(ActorId.Create("same"), ActorId.Parse("same"));
        Assert.NotEqual(ActorId.Create("left"), ActorId.Create("right"));
    }
}
