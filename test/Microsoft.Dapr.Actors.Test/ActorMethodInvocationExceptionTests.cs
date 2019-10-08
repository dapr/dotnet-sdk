// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Test
{
    using System;
    using System.IO;
    using FluentAssertions;
    using Microsoft.Dapr.Actors.Communication;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Contains tests for Actor method invocation exceptions.
    /// </summary>
    [TestClass]
    public class ActorMethodInvocationExceptionTests
    {
        /// <summary>
        /// This test will verify:
        /// 1) the path for serialization and deserialization of the remote exception
        /// 2) and validating the inner exception.
        /// </summary>
        [TestMethod]
        public void TestThrowActorMethodInvocationException()
        {
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
            var ex = this.ThrowRemoteException(message, remoteMethodException);
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
