// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
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

using Moq;

namespace Dapr.SecretsManagement.Test;

public sealed class DaprSecretsManagementGrpcClientTests
{
    [Fact]
    public async Task GetSecretAsync_ThrowsOnNullStoreName()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprSecretsManagementGrpcClient(mockClient, httpClient, null);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.GetSecretAsync(null!, "my-key", cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task GetSecretAsync_ThrowsOnNullKey()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprSecretsManagementGrpcClient(mockClient, httpClient, null);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.GetSecretAsync("my-store", null!, cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task GetSecretAsync_ThrowsOnEmptyKey()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprSecretsManagementGrpcClient(mockClient, httpClient, null);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.GetSecretAsync("my-store", "", cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task GetBulkSecretAsync_ThrowsOnNullStoreName()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprSecretsManagementGrpcClient(mockClient, httpClient, null);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.GetBulkSecretAsync(null!, cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public void Dispose_CanBeCalledWithoutException()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = new HttpClient();

        var client = new DaprSecretsManagementGrpcClient(mockClient, httpClient, null);

        // Should not throw.
        client.Dispose();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = new HttpClient();

        var client = new DaprSecretsManagementGrpcClient(mockClient, httpClient, null);

        // Calling Dispose multiple times should not throw.
        client.Dispose();
        client.Dispose();
    }
}
