// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dapr.Common.Test;

public sealed class DaprRuntimeCapabilitiesTests
{
    private static readonly TimeSpan ReflectionTimeout = TimeSpan.FromMilliseconds(200);

    [Fact]
    public async Task GetMethodSupportAsync_WhenReflectionNeverCompletes_ReturnsUnknown()
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        var reflection = ReflectionService.Hanging();
        await using var server = await ReflectionTestServer.StartAsync(reflection, testCancellationToken);
        var capabilities = server.CreateCapabilities(ReflectionTimeout);
        var stopwatch = Stopwatch.StartNew();

        var support = await capabilities.GetMethodSupportAsync(
            "dapr.proto.runtime.v1.Dapr/BulkPublishEvent",
            testCancellationToken);

        Assert.Equal(DaprRuntimeSupport.Unknown, support);
        Assert.Equal(1, reflection.CallCount);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetMethodSupportAsync_WhenReflectionNeverCompletes_ConcurrentCallersShareOneBoundedLookup()
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        var reflection = ReflectionService.Hanging();
        await using var server = await ReflectionTestServer.StartAsync(reflection, testCancellationToken);
        var capabilities = server.CreateCapabilities(TimeSpan.FromSeconds(2));

        var calls = Enumerable.Range(0, 20)
            .Select(index => capabilities.GetMethodSupportAsync(
                $"dapr.proto.runtime.v1.Dapr/BulkPublishEvent{index}",
                testCancellationToken))
            .ToArray();

        await reflection.RequestReceived.WaitAsync(TimeSpan.FromSeconds(1), testCancellationToken);
        var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(3), testCancellationToken);

        Assert.All(results, result => Assert.Equal(DaprRuntimeSupport.Unknown, result));
        Assert.Equal(1, reflection.CallCount);
    }

    [Fact]
    public async Task GetMethodSupportAsync_WhenOneServiceHangs_DoesNotBlockAnotherServiceLookup()
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        const string hangingService = "dapr.proto.runtime.v1.Dapr";
        const string availableService = "example.v1.Available";
        var reflection = ReflectionService.HangingForService(
            hangingService,
            CreateDescriptor(availableService, "AvailableMethod"));
        await using var server = await ReflectionTestServer.StartAsync(reflection, testCancellationToken);
        var capabilities = server.CreateCapabilities(TimeSpan.FromSeconds(2));

        var hangingLookup = capabilities.GetMethodSupportAsync(
            $"{hangingService}/BulkPublishEvent",
            testCancellationToken);
        await reflection.HangingRequestReceived.WaitAsync(TimeSpan.FromSeconds(1), testCancellationToken);

        var availableLookup = capabilities.GetMethodSupportAsync(
            $"{availableService}/AvailableMethod",
            testCancellationToken);

        await reflection.AvailableRequestReceived.WaitAsync(TimeSpan.FromSeconds(1), testCancellationToken);
        Assert.Equal(DaprRuntimeSupport.Supported, await availableLookup);
        Assert.False(hangingLookup.IsCompleted);
        Assert.Equal(
            DaprRuntimeSupport.Unknown,
            await hangingLookup.WaitAsync(TimeSpan.FromSeconds(3), testCancellationToken));
        Assert.Equal(2, reflection.CallCount);
    }

    [Fact]
    public async Task GetMethodSupportAsync_WhenCallerCancels_DoesNotCancelOrPoisonSharedLookup()
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        const string serviceName = "dapr.proto.runtime.v1.Dapr";
        var reflection = ReflectionService.BlockedUntilReleased(
            CreateDescriptor(serviceName, "BulkPublishEvent"));
        await using var server = await ReflectionTestServer.StartAsync(reflection, testCancellationToken);
        var capabilities = server.CreateCapabilities(TimeSpan.FromSeconds(2));
        using var callerCancellation = new CancellationTokenSource();

        var cancelledLookup = capabilities.GetMethodSupportAsync(
            $"{serviceName}/BulkPublishEvent",
            callerCancellation.Token);
        await reflection.RequestReceived.WaitAsync(TimeSpan.FromSeconds(1), testCancellationToken);
        callerCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledLookup);

        reflection.Release();

        Assert.Equal(
            DaprRuntimeSupport.Supported,
            await capabilities.GetMethodSupportAsync(
                $"{serviceName}/BulkPublishEvent",
                testCancellationToken));
        Assert.Equal(1, reflection.CallCount);
    }

    [Theory]
    [InlineData(StatusCode.Unknown)]
    [InlineData(StatusCode.Unimplemented)]
    [InlineData(StatusCode.Cancelled)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public async Task GetMethodSupportAsync_WhenReflectionReturnsRpcError_CachesUnknownResult(
        StatusCode statusCode)
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        var reflection = ReflectionService.Failing(statusCode);
        await using var server = await ReflectionTestServer.StartAsync(reflection, testCancellationToken);
        var capabilities = server.CreateCapabilities(ReflectionTimeout);

        var first = await capabilities.GetMethodSupportAsync(
            "dapr.proto.runtime.v1.Dapr/BulkPublishEvent",
            testCancellationToken);
        var second = await capabilities.GetMethodSupportAsync(
            "another.Service/PublishEvent",
            testCancellationToken);

        Assert.Equal(DaprRuntimeSupport.Unknown, first);
        Assert.Equal(DaprRuntimeSupport.Unknown, second);
        Assert.Equal(1, reflection.CallCount);
    }

    [Fact]
    public async Task GetMethodSupportAsync_WhenReflectionSucceeds_CachesAndDistinguishesMethods()
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        const string serviceName = "dapr.proto.runtime.v1.Dapr";
        var reflection = ReflectionService.Successful(
            CreateDescriptor(serviceName, "BulkPublishEvent", "PublishEvent"));
        await using var server = await ReflectionTestServer.StartAsync(reflection, testCancellationToken);
        var capabilities = server.CreateCapabilities(ReflectionTimeout);

        var bulkPublishSupport = await capabilities.GetMethodSupportAsync(
            $"{serviceName}/BulkPublishEvent",
            testCancellationToken);
        var publishSupport = await capabilities.GetMethodSupportAsync(
            $"{serviceName}/PublishEvent",
            testCancellationToken);
        var missingSupport = await capabilities.GetMethodSupportAsync(
            $"{serviceName}/MissingMethod",
            testCancellationToken);

        Assert.Equal(DaprRuntimeSupport.Supported, bulkPublishSupport);
        Assert.Equal(DaprRuntimeSupport.Supported, publishSupport);
        Assert.Equal(DaprRuntimeSupport.Unsupported, missingSupport);
        Assert.Equal(1, reflection.CallCount);
    }

    [Fact]
    public async Task GetServiceSupportAsync_WhenReflectionUnavailable_ReturnsUnknownAndCaches()
    {
        var testCancellationToken = TestContext.Current.CancellationToken;
        var reflection = ReflectionService.Hanging();
        await using var server = await ReflectionTestServer.StartAsync(reflection, testCancellationToken);
        var capabilities = server.CreateCapabilities(ReflectionTimeout);

        var first = await capabilities.GetServiceSupportAsync(
            "dapr.proto.runtime.v1.Dapr",
            testCancellationToken);
        var second = await capabilities.GetServiceSupportAsync(
            "another.Service",
            testCancellationToken);

        Assert.Equal(DaprRuntimeSupport.Unknown, first);
        Assert.Equal(DaprRuntimeSupport.Unknown, second);
        Assert.Equal(1, reflection.CallCount);
    }

    private static FileDescriptorProto CreateDescriptor(string fullyQualifiedServiceName, params string[] methods)
    {
        var separator = fullyQualifiedServiceName.LastIndexOf('.');
        var package = separator < 0 ? string.Empty : fullyQualifiedServiceName[..separator];
        var serviceName = separator < 0
            ? fullyQualifiedServiceName
            : fullyQualifiedServiceName[(separator + 1)..];
        var service = new ServiceDescriptorProto { Name = serviceName };
        service.Method.Add(methods.Select(method => new MethodDescriptorProto { Name = method }));

        var descriptor = new FileDescriptorProto
        {
            Name = $"{serviceName.ToLowerInvariant()}.proto",
            Package = package,
        };
        descriptor.Service.Add(service);
        return descriptor;
    }

    private sealed class ReflectionTestServer : IAsyncDisposable
    {
        private readonly IHost host;

        private ReflectionTestServer(IHost host, GrpcChannel channel)
        {
            this.host = host;
            Channel = channel;
        }

        private GrpcChannel Channel { get; }

        public static async Task<ReflectionTestServer> StartAsync(
            ReflectionService reflection,
            CancellationToken cancellationToken)
        {
            var host = await new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddGrpc();
                        services.AddSingleton(reflection);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapGrpcService<ReflectionService>());
                    }))
                .StartAsync(cancellationToken);

            var client = host.GetTestClient();
            client.DefaultRequestVersion = new Version(2, 0);
            var channel = GrpcChannel.ForAddress(
                client.BaseAddress!,
                new GrpcChannelOptions { HttpClient = client });
            return new ReflectionTestServer(host, channel);
        }

        public DaprRuntimeCapabilities CreateCapabilities(TimeSpan timeout)
        {
            return new DaprRuntimeCapabilities(
                new ServerReflection.ServerReflectionClient(Channel),
                timeout);
        }

        public async ValueTask DisposeAsync()
        {
            Channel.Dispose();
            await host.StopAsync(TestContext.Current.CancellationToken);
            host.Dispose();
        }
    }

    private sealed class ReflectionService : ServerReflection.ServerReflectionBase
    {
        private readonly Func<ServerReflectionRequest, IServerStreamWriter<ServerReflectionResponse>,
            ServerCallContext, Task> handleRequest;
        private int callCount;

        private ReflectionService(
            Func<ServerReflectionRequest, IServerStreamWriter<ServerReflectionResponse>,
                ServerCallContext, Task> handleRequest)
        {
            this.handleRequest = handleRequest;
        }

        public int CallCount => Volatile.Read(ref callCount);

        public Task RequestReceived => requestReceived.Task;

        public Task HangingRequestReceived => hangingRequestReceived.Task;

        public Task AvailableRequestReceived => availableRequestReceived.Task;

        private readonly TaskCompletionSource requestReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource hangingRequestReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource availableRequestReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static ReflectionService Hanging()
        {
            return new ReflectionService(async (_, _, context) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
            });
        }

        public static ReflectionService HangingForService(
            string hangingService,
            FileDescriptorProto availableDescriptor)
        {
            return new ReflectionService(async (request, responseStream, context) =>
            {
                if (request.FileContainingSymbol == hangingService)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                    return;
                }

                await WriteDescriptorAsync(responseStream, availableDescriptor);
            });
        }

        public static ReflectionService BlockedUntilReleased(FileDescriptorProto descriptor)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = new ReflectionService(async (_, responseStream, context) =>
            {
                await release.Task.WaitAsync(context.CancellationToken);
                await WriteDescriptorAsync(responseStream, descriptor);
            });
            service.release = release;
            return service;
        }

        public static ReflectionService Failing(StatusCode statusCode)
        {
            return new ReflectionService((_, _, _) =>
                throw new RpcException(new Status(statusCode, "Reflection unavailable.")));
        }

        public static ReflectionService Successful(FileDescriptorProto descriptor)
        {
            return new ReflectionService((_, responseStream, _) =>
                WriteDescriptorAsync(responseStream, descriptor));
        }

        public void Release()
        {
            release.TrySetResult();
        }

        public override async Task ServerReflectionInfo(
            IAsyncStreamReader<ServerReflectionRequest> requestStream,
            IServerStreamWriter<ServerReflectionResponse> responseStream,
            ServerCallContext context)
        {
            Interlocked.Increment(ref callCount);

            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var request = requestStream.Current;
                requestReceived.TrySetResult();
                if (request.FileContainingSymbol == "dapr.proto.runtime.v1.Dapr")
                {
                    hangingRequestReceived.TrySetResult();
                }
                else
                {
                    availableRequestReceived.TrySetResult();
                }

                await handleRequest(request, responseStream, context);
            }
        }

        private static Task WriteDescriptorAsync(
            IServerStreamWriter<ServerReflectionResponse> responseStream,
            FileDescriptorProto descriptor)
        {
            var response = new ServerReflectionResponse
            {
                FileDescriptorResponse = new FileDescriptorResponse(),
            };
            response.FileDescriptorResponse.FileDescriptorProto.Add(descriptor.ToByteString());
            return responseStream.WriteAsync(response);
        }
    }
}
