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

using System.Text.Json;
using Shouldly;
using Xunit;

namespace Dapr.VirtualActors.Abstractions.Test;

public class VirtualActorIdJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public VirtualActorIdJsonConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new VirtualActorIdJsonConverter());
    }

    [Fact]
    public void Serialize_WritesStringValue()
    {
        var id = new VirtualActorId("actor-123");
        var json = JsonSerializer.Serialize(id, _options);
        json.ShouldBe("\"actor-123\"");
    }

    [Fact]
    public void Deserialize_ReadsStringValue()
    {
        var json = "\"actor-456\"";
        var id = JsonSerializer.Deserialize<VirtualActorId>(json, _options);
        id.GetId().ShouldBe("actor-456");
    }

    [Fact]
    public void RoundTrip_PreservesValue()
    {
        var original = new VirtualActorId("round-trip-test");
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<VirtualActorId>(json, _options);
        deserialized.ShouldBe(original);
    }
}
