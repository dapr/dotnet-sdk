// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.DistributedLock.Models;

namespace Dapr.DistributedLock.Test.Models;

public class UnlockResponseTest
{
    [Fact]
    public void UnlockResponse_SetsStatus()
    {
        var response = new UnlockResponse(LockStatus.Success);
        Assert.Equal(LockStatus.Success, response.Status);
    }

    [Theory]
    [InlineData(LockStatus.Success)]
    [InlineData(LockStatus.LockDoesNotExist)]
    [InlineData(LockStatus.LockBelongsToOthers)]
    [InlineData(LockStatus.InternalError)]
    public void UnlockResponse_SetsEachStatus(LockStatus status)
    {
        var response = new UnlockResponse(status);
        Assert.Equal(status, response.Status);
    }

    [Fact]
    public void UnlockResponse_EqualityByStatus()
    {
        var r1 = new UnlockResponse(LockStatus.Success);
        var r2 = new UnlockResponse(LockStatus.Success);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void UnlockResponse_InequalityByStatus()
    {
        var r1 = new UnlockResponse(LockStatus.Success);
        var r2 = new UnlockResponse(LockStatus.LockDoesNotExist);
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void UnlockResponse_StatusPropertyMatchesConstructorArg()
    {
        var response = new UnlockResponse(LockStatus.LockBelongsToOthers);
        Assert.Equal(LockStatus.LockBelongsToOthers, response.Status);
    }
}
