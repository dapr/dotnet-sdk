// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dapr;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;

namespace Dapr.Client;

public partial class DaprClientTest
{
    [Fact]
    public void CreateInvokeHttpClient_WithAppId()
    {
        var client = DaprClient.CreateInvokeHttpClient(appId: "bank", daprEndpoint: "http://localhost:3500");
        Assert.Equal("http://bank/", client.BaseAddress.AbsoluteUri);
    }

    [Fact]
    public void CreateInvokeHttpClient_InvalidAppId()
    {
        var ex = Assert.Throws<ArgumentException>(() => 
        { 
            // The appId needs to be something that can be used as hostname in a URI.
            _ = DaprClient.CreateInvokeHttpClient(appId: "");
        });

        Assert.Contains("The appId must be a valid hostname.", ex.Message);
        Assert.IsType<UriFormatException>(ex.InnerException);
    }

    [Fact]
    public void CreateInvokeHttpClient_WithoutAppId()
    {
        var client = DaprClient.CreateInvokeHttpClient(daprEndpoint: "http://localhost:3500");
        Assert.Null(client.BaseAddress);
    }
        
    [Fact]
    public void CreateInvokeHttpClient_InvalidDaprEndpoint_InvalidFormat()
    {
        Assert.Throws<UriFormatException>(() => 
        { 
            _ = DaprClient.CreateInvokeHttpClient(daprEndpoint: "");
        });

        // Exception message comes from the runtime, not validating it here.
    }

    [Fact]
    public void CreateInvokeHttpClient_InvalidDaprEndpoint_InvalidScheme()
    {
        var ex = Assert.Throws<ArgumentException>(() => 
        { 
            _ = DaprClient.CreateInvokeHttpClient(daprEndpoint: "ftp://localhost:3500");
        });

        Assert.Contains("The URI scheme of the Dapr endpoint must be http or https.", ex.Message);
    }

    [Fact]
    public async Task CreateInvocationInvoker_UsesGrpcChannelOptions()
    {
        await using var testClient = TestClient.CreateForMessageHandler();
        using var httpClient = new HttpClient(testClient.InnerClient);
        var invoker = DaprClient.CreateInvocationInvoker(
            appId: "bank",
            grpcChannelOptions: new GrpcChannelOptions { HttpClient = httpClient },
            daprEndpoint: "http://localhost");
        var method = new Method<string, string>(
            MethodType.Unary,
            "test.Service",
            "TestMethod",
            Marshallers.StringMarshaller,
            Marshallers.StringMarshaller);

        var request = await testClient.CaptureGrpcRequestAsync(
            _ => invoker.AsyncUnaryCall(method, null, new CallOptions(), "request").ResponseAsync);

        Assert.Equal("bank", request.Request.Headers.GetValues("dapr-app-id").Single());
        request.Dismiss();
    }

    [Fact]
    public void CreateInvocationInvoker_WithNullGrpcChannelOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => DaprClient.CreateInvocationInvoker(grpcChannelOptions: null!, appId: "bank"));
    }

    [Fact]
    public void GetDaprApiTokenHeader_ApiTokenSet_SetsApiTokenHeader()
    {
        var token = "test_token";
        var entry = DaprClient.GetDaprApiTokenHeader(token);
        Assert.NotNull(entry);
        Assert.Equal("test_token", entry.Value.Value);
    }

    [Fact]
    public void GetDaprApiTokenHeader_ApiTokenNotSet_NullApiTokenHeader()
    {
        var entry = DaprClient.GetDaprApiTokenHeader(null);
        Assert.Equal(default, entry);
    }

    [Fact]
    public async Task TestShutdownApi()
    {
        await using var client = TestClient.CreateForDaprClient();

        var request = await client.CaptureGrpcRequestAsync(async daprClient =>
        {
            await daprClient.ShutdownSidecarAsync();
        });

        request.Dismiss();
    }
}