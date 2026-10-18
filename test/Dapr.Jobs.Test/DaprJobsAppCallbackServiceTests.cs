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
using System.Text;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dapr.Jobs.Test;

public class DaprJobsAppCallbackServiceTest
{
    [Fact]
    public async Task OnJobEvent_InvokesHandlerWithJobNameAndPayload()
    {
        string? capturedJobName = null;
        ReadOnlyMemory<byte> capturedPayload = default;

        var registry = new DaprJobsHandlerRegistry
        {
            Handler = (string jobName, ReadOnlyMemory<byte> payload) =>
            {
                capturedJobName = jobName;
                capturedPayload = payload;
                return Task.CompletedTask;
            }
        };

        var services = new ServiceCollection().BuildServiceProvider();
        var handler = new DaprJobsAppCallbackHandler(registry, services);
        var service = new DaprJobsAppCallbackV1Service(handler);

        var payloadBytes = Encoding.UTF8.GetBytes("test-payload");
        var request = new JobEventRequest
        {
            Name = "myJob",
            Data = new Any
            {
                Value = ByteString.CopyFrom(payloadBytes),
                TypeUrl = "dapr.io/schedule/jobpayload"
            }
        };

        var context = new Mock<ServerCallContext>().Object;
        var response = await service.OnJobEvent(request, context);

        Assert.NotNull(response);
        Assert.Equal("myJob", capturedJobName);
        Assert.Equal(payloadBytes, capturedPayload.ToArray());
    }

    [Fact]
    public async Task OnJobEvent_ResolvesServicesFromDependencyInjection()
    {
        var testService = new TestInjectableService();
        var registry = new DaprJobsHandlerRegistry
        {
            Handler = (string _, ReadOnlyMemory<byte> _, TestInjectableService svc) =>
            {
                svc.WasInvoked = true;
                return Task.CompletedTask;
            }
        };

        var services = new ServiceCollection()
            .AddSingleton(testService)
            .BuildServiceProvider();

        var handler = new DaprJobsAppCallbackHandler(registry, services);
        var service = new DaprJobsAppCallbackV1Service(handler);

        var request = new JobEventRequest
        {
            Name = "diJob",
            Data = new Any
            {
                Value = ByteString.CopyFrom(Encoding.UTF8.GetBytes("data")),
                TypeUrl = "dapr.io/schedule/jobpayload"
            }
        };

        var context = new Mock<ServerCallContext>().Object;
        await service.OnJobEvent(request, context);

        Assert.True(testService.WasInvoked);
    }

    [Fact]
    public async Task OnJobEvent_HandlesNullPayload()
    {
        ReadOnlyMemory<byte> capturedPayload = new byte[] { 0xFF };

        var registry = new DaprJobsHandlerRegistry
        {
            Handler = (string _, ReadOnlyMemory<byte> payload) =>
            {
                capturedPayload = payload;
                return Task.CompletedTask;
            }
        };

        var services = new ServiceCollection().BuildServiceProvider();
        var handler = new DaprJobsAppCallbackHandler(registry, services);
        var service = new DaprJobsAppCallbackV1Service(handler);

        var request = new JobEventRequest { Name = "emptyJob" };

        var context = new Mock<ServerCallContext>().Object;
        await service.OnJobEvent(request, context);

        Assert.True(capturedPayload.IsEmpty);
    }

    [Fact]
    public async Task OnJobEvent_ThrowsWhenNoHandlerConfigured()
    {
        var registry = new DaprJobsHandlerRegistry();
        var services = new ServiceCollection().BuildServiceProvider();
        var handler = new DaprJobsAppCallbackHandler(registry, services);
        var service = new DaprJobsAppCallbackV1Service(handler);

        var request = new JobEventRequest { Name = "noHandler" };
        var context = new Mock<ServerCallContext>().Object;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.OnJobEvent(request, context));
    }

    internal sealed class TestInjectableService
    {
        public bool WasInvoked { get; set; }
    }
}
