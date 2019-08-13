// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Test
{
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Actions.Actors.Client;

    [TestClass]
    public class ActorCommunicationTests
    {
        [TestMethod]
        public void TestCreateHttpActorCommunicationClientFactory()
        {
            var factory = new HttpActorCommunicationClientFactory();
        }

        [TestMethod]
        public void TestInvokingMethodOnActorInterface()
        {
            var actorId = new ActorId("Test");
            var proxy = ActorProxy.Create<ITestActor>(actorId, typeof(TestActor));
            proxy.SetCountAsync(5, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
