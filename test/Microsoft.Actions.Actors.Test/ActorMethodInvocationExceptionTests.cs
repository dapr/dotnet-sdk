// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Test
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Actions.Actors.Communication;
    using FluentAssertions;

    [TestClass]
    public class ActorMethodInvocationExceptionTests
    {
        [TestMethod]
        public void TestThrowActorMethodInvocationException()
        {
            // This test will test 
            // 1) the path for serialization and deserialization of the remote exception
            // 2) and validating the inner exception

            // Create Remote Actor Method test Exception
            var exception = new InvalidOperationException();
            var message = "Remote Actor Exception";

            // Create Serialized Exception
            var serializedException = RemoteException.FromException(new InvalidOperationException());

            // De Serialize Exception
            var isDeserialzied = RemoteException.ToException(
                                                     new MemoryStream(serializedException),
                                                     out var remoteMethodException);
            isDeserialzied.Should().BeTrue();
            var ex = ThrowRemoteException(message, remoteMethodException);
            ex.Should().BeOfType<ActorMethodInvocationException>();
            ex.InnerException.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(message);
        }

        private Exception ThrowRemoteException(string message, Exception exception)
        {
            return new ActorMethodInvocationException(message, exception, false);
        }
    }
}
