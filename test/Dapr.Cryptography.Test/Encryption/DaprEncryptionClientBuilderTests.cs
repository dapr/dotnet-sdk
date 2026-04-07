// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System;
using Dapr.Cryptography.Encryption;

namespace Dapr.Cryptography.Test.Encryption;

public class DaprEncryptionClientBuilderTests
{
    [Fact]
    public void Build_ReturnsNonNullClient()
    {
        var builder = new DaprEncryptionClientBuilder();
        using var client = builder.Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_ReturnsDaprEncryptionClient()
    {
        var builder = new DaprEncryptionClientBuilder();
        using var client = builder.Build();
        Assert.IsAssignableFrom<DaprEncryptionClient>(client);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_DoesNotThrow()
    {
        var builder = new DaprEncryptionClientBuilder(null);
        Assert.NotNull(builder);
    }

    [Fact]
    public void Build_CalledMultipleTimes_ReturnsNewInstanceEachTime()
    {
        var builder = new DaprEncryptionClientBuilder();
        using var client1 = builder.Build();
        using var client2 = builder.Build();
        Assert.NotSame(client1, client2);
    }
}
