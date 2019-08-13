// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Test
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Actions.Actors.Builder;

    [TestClass]
    public class ActorCodeBuilderTests
    {
        [TestMethod]
        public void TestBuildActorProxyGenerator()
        {
            ActorProxyGenerator proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(typeof(ITestActor));
        }
    }
}
