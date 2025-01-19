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

namespace Dapr.Actors.Test;

using System.Threading.Tasks;
using Dapr.Actors.Builder;
using Dapr.Actors.Communication;
using Dapr.Actors.Description;
using Dapr.Actors.Runtime;
using Xunit;

/// <summary>
/// Test class for Actor Code builder.
/// </summary>
public class ActorCodeBuilderTests
{
    /// <summary>
    /// Tests Proxy Generation.
    /// </summary>
    [Fact]
    public void TestBuildActorProxyGenerator()
    {
        ActorCodeBuilder.GetOrCreateProxyGenerator(typeof(ITestActor));
    }

    [Fact]
    public async Task ActorCodeBuilder_BuildDispatcher()
    {
        var host = ActorHost.CreateForTest<TestActor>();

        var dispatcher = ActorCodeBuilder.GetOrCreateMethodDispatcher(typeof(ITestActor));
        var methodId = MethodDescription.Create("test", typeof(ITestActor).GetMethod("GetCountAsync"), true).Id;

        var impl = new TestActor(host);
        var request = new ActorRequestMessageBody(0);
        var response = new WrappedRequestMessageFactory();

        var body = (WrappedMessage)await dispatcher.DispatchAsync(impl, methodId, request, response, default);
        dynamic bodyValue = body.Value;
        Assert.Equal(5, (int)bodyValue.retVal);
    }
}
