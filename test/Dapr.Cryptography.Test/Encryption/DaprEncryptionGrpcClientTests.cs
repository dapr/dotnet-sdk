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
using System.IO;
using System.Net.Http;
using System.Threading;
using Dapr.Cryptography.Encryption;
using Moq;
// ReSharper disable UnusedVariable

namespace Dapr.Cryptography.Test.Encryption;

public class DaprEncryptionGrpcClientTests
{
    [Fact]
    public void EncryptAsync_VaultNameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprEncryptionGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            var bytes = Array.Empty<byte>();
            await client.EncryptAsync(string.Empty, bytes, "key", new EncryptionOptions(KeyWrapAlgorithm.A128cbc), CancellationToken.None);

        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void EncryptAsync_KeyNameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprEncryptionGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            var bytes = Array.Empty<byte>();
            await client.EncryptAsync("myVault", bytes, string.Empty, new EncryptionOptions(KeyWrapAlgorithm.A128cbc), CancellationToken.None);

        });
#pragma warning restore CS0618 // Type or member is obsolete
    }
    
    [Fact]
    public void EncryptAsync_StreamCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprEncryptionGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            await client.EncryptAsync("myVault", (Stream)null, string.Empty, new EncryptionOptions(KeyWrapAlgorithm.A128cbc), CancellationToken.None);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void EncryptAsync_OptionsCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprEncryptionGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            await client.EncryptAsync("myVault", Array.Empty<byte>(), string.Empty, null, CancellationToken.None);
        });
#pragma warning restore CS0618 // Type or member is obsolete        
    }
}
