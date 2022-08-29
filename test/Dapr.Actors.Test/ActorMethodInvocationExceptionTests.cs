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

namespace Dapr.Actors.Test
{
    using System;
    using System.IO;
    using Dapr.Actors.Communication;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Contains tests for Actor method invocation exceptions.
    /// </summary>
    public class ActorMethodInvocationExceptionTests
    {
        /// <summary>
        /// This test will verify:
        /// 1) the path for serialization and deserialization of the remote exception
        /// 2) and validating the inner exception.
        /// </summary>
        [Fact]
        public void TestThrowActorMethodInvocationException()
        {
            // Create Remote Actor Method test Exception
            var message = "Remote Actor encountered an exception";
            var innerMessage = "Bad time zone";
            var exception = new InvalidOperationException(message, new InvalidTimeZoneException(innerMessage));

            // Create Serialized Exception
            var serializedException = ActorInvokeException.FromException(exception);

            // De Serialize Exception
            var isDeserialzied = ActorInvokeException.ToException(
                                                     new MemoryStream(serializedException),
                                                     out var remoteMethodException);
            isDeserialzied.Should().BeTrue();
            var ex = this.ThrowRemoteException(message, remoteMethodException);
            ex.Should().BeOfType<ActorMethodInvocationException>();
            ex.InnerException.Should().BeOfType<ActorInvokeException>();
            ((ActorInvokeException)ex.InnerException).ActualExceptionType.Should().Be("System.InvalidOperationException");
            ex.InnerException.InnerException.Should().BeNull();
            ex.Message.Should().Be(message);
            ex.InnerException.Message.Should().Be(message);
        }

        private Exception ThrowRemoteException(string message, Exception exception)
        {
            return new ActorMethodInvocationException(message, exception, false);
        }
    }
}
