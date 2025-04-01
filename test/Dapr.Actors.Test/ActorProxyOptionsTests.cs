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

namespace Dapr.Actors.Client;

using System;
using Shouldly;
using Xunit;

/// <summary>
/// Test class for Actor Code builder.
/// </summary>
public class ActorProxyOptionsTests
{
    [Fact]
    public void DefaultConstructor_Succeeds()
    {
        var options = new ActorProxyOptions();
        Assert.NotNull(options);
    }

    [Fact]
    public void SerializerOptionsCantBeNull_Fails()
    {
        var options = new ActorProxyOptions();
        Action action = () => options.JsonSerializerOptions = null;

        action.ShouldThrow<ArgumentNullException>();
    }
}
