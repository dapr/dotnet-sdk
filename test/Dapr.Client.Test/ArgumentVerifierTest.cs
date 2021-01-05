// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using Xunit;

    public class ArgumentVerifierTest
    {
        [Fact]
        public void ThrowIfNull_RespectsArgumentName()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    ArgumentVerifier.ThrowIfNull(null, "args");
                });

            Assert.Contains("args", ex.Message);
        }

        [Fact]
        public void ThrowIfNullOrEmpty_RespectsArgumentName_WhenValueIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    ArgumentVerifier.ThrowIfNullOrEmpty(null, "args");
                });

            Assert.Contains("args", ex.Message);
        }

        [Fact]
        public void ThrowIfNullOrEmpty_RespectsArgumentName_WhenValueIsEmpty()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                {
                    ArgumentVerifier.ThrowIfNullOrEmpty(string.Empty, "args");
                });

            Assert.Contains("args", ex.Message);
        }
    }
}
