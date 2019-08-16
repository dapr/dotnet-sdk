// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Test
{
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Actions.Actors.Client;
    using Microsoft.Actions.Actors.Communication.Client;

    [TestClass]
    public class ActorCommunicationTests
    {
        [TestMethod]
        public void TestCreateActorCommunicationClientFactory()
        {
            var factory = new ActorCommunicationClientFactory();
        }

        [TestMethod]
        public void TestInvokingMethodOnActorInterface()
        {
            var actorId = new ActorId("Test");
            var proxy = ActorProxy.Create<ITestActor>(actorId, typeof(TestActor));
            // TODO the following call is expected to fail as http send request will fail till we have mocked it.
            proxy.SetCountAsync(5, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
