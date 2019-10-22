// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using Dapr.Actors.Builder;
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
    }
}
