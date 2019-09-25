// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Test
{
    using Microsoft.Actions.Actors.Builder;
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
