// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Test
{
    using Microsoft.Dapr.Actors.Builder;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test class for Actor Code builder.
    /// </summary>
    [TestClass]
    public class ActorCodeBuilderTests
    {
        /// <summary>
        /// Tests Proxy Generation.
        /// </summary>
        [TestMethod]
        public void TestBuildActorProxyGenerator()
        {
            ActorProxyGenerator proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(typeof(ITestActor));
        }
    }
}
