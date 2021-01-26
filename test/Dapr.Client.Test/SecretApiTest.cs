﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Google.Rpc;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Moq;
    using Xunit;
    using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

    public class SecretApiTest
    {
        [Fact]
        public async Task GetSecretAsync_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var task = daprClient.GetSecretAsync("testStore", "test_key", metadata);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<Autogenerated.GetSecretRequest>(entry.Request);
            request.StoreName.Should().Be("testStore");
            request.Key.Should().Be("test_key");
            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task GetSecretAsync_ReturnSingleSecret()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var task = daprClient.GetSecretAsync("testStore", "test_key", metadata);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<Autogenerated.GetSecretRequest>(entry.Request);
            request.StoreName.Should().Be("testStore");
            request.Key.Should().Be("test_key");
            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>
            {
                { "redis_secret", "Guess_Redis" }
            };
            await SendResponseWithSecrets(secrets, entry);

            // Get response and validate
            var secretsResponse = await task;
            secretsResponse.Count.Should().Be(1);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"].Should().Be("Guess_Redis");
        }

        [Fact]
        public async Task GetSecretAsync_ReturnMultipleSecrets()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var task = daprClient.GetSecretAsync("testStore", "test_key", metadata);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<Autogenerated.GetSecretRequest>(entry.Request);
            request.StoreName.Should().Be("testStore");
            request.Key.Should().Be("test_key");
            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>
            {
                { "redis_secret", "Guess_Redis" },
                { "kafka_secret", "Guess_Kafka" }
            };
            await SendResponseWithSecrets(secrets, entry);

            // Get response and validate
            var secretsResponse = await task;
            secretsResponse.Count.Should().Be(2);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"].Should().Be("Guess_Redis");
            secretsResponse.ContainsKey("kafka_secret").Should().BeTrue();
            secretsResponse["kafka_secret"].Should().Be("Guess_Kafka");
        }

        [Fact]
        public async Task GetSecretAsync_WithCancelledToken()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();
            await FluentActions.Awaiting(async () => await daprClient.GetSecretAsync("testStore", "test_key", metadata, cancellationToken: ct))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task GetSecretAsync_WrapsRpcException()
        {
            var client = new MockClient();

            var rpcStatus = new Grpc.Core.Status(StatusCode.Internal, "not gonna work");
            var rpcException = new RpcException(rpcStatus, new Metadata(), "not gonna work");

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.GetSecretAsync(It.IsAny<Autogen.Grpc.v1.GetSecretRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await client.DaprClient.GetSecretAsync("test", "test");
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task GetBulkSecretAsync_ValidateRequest()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
            var task = daprClient.GetBulkSecretAsync("testStore", metadata);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<Autogenerated.GetBulkSecretRequest>(entry.Request);
            request.StoreName.Should().Be("testStore");
            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task GetBulkSecretAsync_ReturnSingleSecret()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
            var task = daprClient.GetBulkSecretAsync("testStore", metadata);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<Autogenerated.GetBulkSecretRequest>(entry.Request);
            request.StoreName.Should().Be("testStore");
            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>();
            secrets.Add("redis_secret", "Guess_Redis");
            await SendBulkResponseWithSecrets(secrets, entry);

            // Get response and validate
            var secretsResponse = await task;
            secretsResponse.Count.Should().Be(1);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"]["redis_secret"].Should().Be("Guess_Redis");
        }

        [Fact]
        public async Task GetBulkSecretAsync_ReturnMultipleSecrets()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
            var task = daprClient.GetBulkSecretAsync("testStore", metadata);

            // Get Request and validate
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            var request = await GrpcUtils.GetRequestFromRequestMessageAsync<Autogenerated.GetBulkSecretRequest>(entry.Request);
            request.StoreName.Should().Be("testStore");
            request.Metadata.Count.Should().Be(2);
            request.Metadata.Keys.Contains("key1").Should().BeTrue();
            request.Metadata.Keys.Contains("key2").Should().BeTrue();
            request.Metadata["key1"].Should().Be("value1");
            request.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>();
            secrets.Add("redis_secret", "Guess_Redis");
            secrets.Add("kafka_secret", "Guess_Kafka");
            await SendBulkResponseWithSecrets(secrets, entry);

            // Get response and validate
            var secretsResponse = await task;
            secretsResponse.Count.Should().Be(2);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"]["redis_secret"].Should().Be("Guess_Redis");
            secretsResponse.ContainsKey("kafka_secret").Should().BeTrue();
            secretsResponse["kafka_secret"]["kafka_secret"].Should().Be("Guess_Kafka");
        }

        [Fact]
        public async Task GetBulkSecretAsync_WithCancelledToken()
        {
            // Configure Client
            var httpClient = new TestHttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");

            var ctSource = new CancellationTokenSource();
            CancellationToken ct = ctSource.Token;
            ctSource.Cancel();
            await FluentActions.Awaiting(async () => await daprClient.GetBulkSecretAsync("testStore", metadata, cancellationToken: ct))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task GetBulkSecretAsync_WrapsRpcException()
        {
            var client = new MockClient();

            var rpcStatus = new Grpc.Core.Status(StatusCode.Internal, "not gonna work");
            var rpcException = new RpcException(rpcStatus, new Metadata(), "not gonna work");

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.GetBulkSecretAsync(It.IsAny<Autogen.Grpc.v1.GetBulkSecretRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await client.DaprClient.GetBulkSecretAsync("test");
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        private async Task SendResponseWithSecrets(Dictionary<string, string> secrets, TestHttpClient.Entry entry)
        {
            var secretResponse = new Autogenerated.GetSecretResponse();
            secretResponse.Data.Add(secrets);

            var streamContent = await GrpcUtils.CreateResponseContent(secretResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
        }

        private async Task SendBulkResponseWithSecrets(Dictionary<string, string> secrets, TestHttpClient.Entry entry)
        {
            var getBulkSecretResponse = new Autogenerated.GetBulkSecretResponse();
            foreach (var secret in secrets)
            {
                var secretsResponse = new Autogenerated.SecretResponse();
                secretsResponse.Secrets[secret.Key] = secret.Value;
                getBulkSecretResponse.Data.Add(secret.Key, secretsResponse);
            }

            var streamContent = await GrpcUtils.CreateResponseContent(getBulkSecretResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
        }
    }
}
