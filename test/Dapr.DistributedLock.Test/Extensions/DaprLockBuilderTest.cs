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

using Dapr.DistributedLock.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.DistributedLock.Test.Extensions;

public class DaprLockBuilderTest
{
    [Fact]
    public void DaprLockBuilder_StoresServices()
    {
        var services = new ServiceCollection();
        var builder = new DaprLockBuilder(services);

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void DaprLockBuilder_ImplementsIDaprDistributedLockBuilder()
    {
        var services = new ServiceCollection();
        var builder = new DaprLockBuilder(services);

        Assert.IsAssignableFrom<IDaprDistributedLockBuilder>(builder);
    }
}
