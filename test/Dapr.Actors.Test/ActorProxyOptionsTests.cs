// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    using System;
    using System.Text.Json;
    using Dapr.Actors.Builder;
    using Dapr.Actors.Client;
    using Dapr.Actors.Test;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Test class for Actor Code builder.
    /// </summary>
    public class ActorProxyOptionsTests
    {
        [Fact]
        public void DefaultConstructor_Succeeds()
        {
            var options = new ActorProxyOptions();
            Assert.NotNull(options);
        }

        [Fact]
        public void SerializerOptionsCantBeNull_Fails()
        {
            var options = new ActorProxyOptions();
            Action action = () => options.JsonSerializerOptions = null;

            action.Should().Throw<ArgumentNullException>();
        }
    }
}
