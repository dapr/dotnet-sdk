// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System.Threading;
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
            ActorProxyGenerator proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(typeof(ITestActor));
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
}
