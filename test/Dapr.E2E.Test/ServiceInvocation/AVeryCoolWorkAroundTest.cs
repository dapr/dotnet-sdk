// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
// This test has been added as a workaround for https://github.com/NasAmin/trx-parser/issues/111
// to report dummy test results in Dapr.E2E.Test.dll
// Can delete this once this bug is fixed
namespace Dapr.E2E.WorkAround
{
    using Xunit;

    public class WorkAroundTests
    {
        [Fact]
        public void AVeryCoolWorkAroundTest()
        {
            Assert.True(true);
        }
    }
}