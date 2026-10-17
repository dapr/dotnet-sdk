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

#nullable enable

using System;
using System.Linq;
using Dapr.BinaryStore.Extensions;
using Dapr.Testcontainers.Xunit.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.BinaryStore.Test;

[BinaryStore("blobstore")]
public partial interface ITestBlobStore : IDaprBinaryStoreClient;

public class BinaryStoreGeneratorTests
{
    [MinimumDaprRuntimeFact("1.19")]
    public void Generated_Extension_RegistersInterfaceAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddDaprBinaryStoreClient().WithTestBlobStore();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITestBlobStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [MinimumDaprRuntimeFact("1.19")]
    public void Generated_Extension_ResolvesStoreBoundClient()
    {
        var services = new ServiceCollection();
        services.AddDaprBinaryStoreClient().WithTestBlobStore();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITestBlobStore>());
    }

    [MinimumDaprRuntimeFact("1.19")]
    public void Generated_Extension_ReturnsSameBuilder()
    {
        var builder = new ServiceCollection().AddDaprBinaryStoreClient();
        Assert.Same(builder, builder.WithTestBlobStore());
    }

    [MinimumDaprRuntimeFact("1.19")]
    public void BinaryStoreAttribute_ExposesStoreName()
    {
        Assert.Equal("blobstore", new BinaryStoreAttribute("blobstore").StoreName);
    }

    [MinimumDaprRuntimeFact("1.19")]
    public void BinaryStoreAttribute_NullOrEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BinaryStoreAttribute(""));
        Assert.ThrowsAny<ArgumentException>(() => new BinaryStoreAttribute(null!));
    }
}
