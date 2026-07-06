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
using System.Threading;
using System.Threading.Tasks;
using Dapr.DistributedLock.Models;
using Moq;

namespace Dapr.DistributedLock.Test.Models;

public class LockResponseTest
{
    [Fact]
    public void LockResponse_SetsResourceId()
    {
        var mockClient = new Mock<IDaprDistributedLockClient>();
        var response = new LockResponse(mockClient.Object)
        {
            ResourceId = "my-resource",
            LockOwner = "owner",
            StoreName = "store"
        };
        Assert.Equal("my-resource", response.ResourceId);
    }

    [Fact]
    public void LockResponse_SetsLockOwner()
    {
        var mockClient = new Mock<IDaprDistributedLockClient>();
        var response = new LockResponse(mockClient.Object)
        {
            ResourceId = "resource",
            LockOwner = "my-owner",
            StoreName = "store"
        };
        Assert.Equal("my-owner", response.LockOwner);
    }

    [Fact]
    public void LockResponse_SetsStoreName()
    {
        var mockClient = new Mock<IDaprDistributedLockClient>();
        var response = new LockResponse(mockClient.Object)
        {
            ResourceId = "resource",
            LockOwner = "owner",
            StoreName = "my-store"
        };
        Assert.Equal("my-store", response.StoreName);
    }

    [Fact]
    public async Task LockResponse_DisposeAsync_CallsTryUnlockAsync()
    {
        var mockClient = new Mock<IDaprDistributedLockClient>();
        mockClient
            .Setup(c => c.TryUnlockAsync("my-store", "my-resource", "my-owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnlockResponse(LockStatus.Success));

        var response = new LockResponse(mockClient.Object)
        {
            ResourceId = "my-resource",
            LockOwner = "my-owner",
            StoreName = "my-store"
        };

        await response.DisposeAsync();

        mockClient.Verify(
            c => c.TryUnlockAsync("my-store", "my-resource", "my-owner", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LockResponse_DisposeAsync_PassesCorrectArguments()
    {
        const string storeName = "lock-store";
        const string resourceId = "resource-id";
        const string lockOwner = "owner-id";

        string? capturedStore = null;
        string? capturedResource = null;
        string? capturedOwner = null;

        var mockClient = new Mock<IDaprDistributedLockClient>();
        mockClient
            .Setup(c => c.TryUnlockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((s, r, o, _) =>
            {
                capturedStore = s;
                capturedResource = r;
                capturedOwner = o;
            })
            .ReturnsAsync(new UnlockResponse(LockStatus.Success));

        var response = new LockResponse(mockClient.Object)
        {
            ResourceId = resourceId,
            LockOwner = lockOwner,
            StoreName = storeName
        };

        await response.DisposeAsync();

        Assert.Equal(storeName, capturedStore);
        Assert.Equal(resourceId, capturedResource);
        Assert.Equal(lockOwner, capturedOwner);
    }

    [Fact]
    public void LockResponse_ImplementsIAsyncDisposable()
    {
        var mockClient = new Mock<IDaprDistributedLockClient>();
        var response = new LockResponse(mockClient.Object)
        {
            ResourceId = "r",
            LockOwner = "o",
            StoreName = "s"
        };
        Assert.IsAssignableFrom<System.IAsyncDisposable>(response);
    }
}
