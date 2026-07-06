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

using System;
using Dapr.DistributedLock.Models;

namespace Dapr.DistributedLock.Test.Models;

public class LockStatusTest
{
    [Fact]
    public void LockStatus_Success_HasValue0()
    {
        Assert.Equal(0, (int)LockStatus.Success);
    }

    [Fact]
    public void LockStatus_LockDoesNotExist_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(LockStatus), LockStatus.LockDoesNotExist));
    }

    [Fact]
    public void LockStatus_LockBelongsToOthers_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(LockStatus), LockStatus.LockBelongsToOthers));
    }

    [Fact]
    public void LockStatus_InternalError_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(LockStatus), LockStatus.InternalError));
    }

    [Fact]
    public void LockStatus_HasExactlyFourValues()
    {
        var values = Enum.GetValues<LockStatus>();
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void LockStatus_DefaultValue_IsSuccess()
    {
        var defaultStatus = default(LockStatus);
        Assert.Equal(LockStatus.Success, defaultStatus);
    }
}
