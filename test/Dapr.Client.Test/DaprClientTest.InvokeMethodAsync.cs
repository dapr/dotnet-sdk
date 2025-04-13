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

using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace Dapr.Client.Test;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using Xunit;

// Most of the InvokeMethodAsync functionality on DaprClient is non-abstract methods that
// forward to a few different request points to create a message, or send a message and process
// its result.
//
// So we write basic tests for all of those that every parameter passing is correct, and then
// test the specialized methods in detail.
public partial class DaprClientTest
{
    private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        // Use case sensitive settings for tests, this way we verify that the same settings are being
        // used in all calls.
        PropertyNameCaseInsensitive  = false,
    };

    [Fact]
    public async Task InvokeMethodAsync_VoidVoidNoHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            await daprClient.InvokeMethodAsync("app1", "mymethod");
        });

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Post);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);
        Assert.Null(request.Request.Content);

        await request.CompleteAsync(new HttpResponseMessage());
    }

    [Fact]
    public async Task InvokeMethodAsync_VoidVoidWithHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            await daprClient.InvokeMethodAsync(HttpMethod.Put, "app1", "mymethod");
        });

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Put);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);
        Assert.Null(request.Request.Content);

        await request.CompleteAsync(new HttpResponseMessage());
    }

    [Fact]
    public async Task InvokeMethodAsync_VoidResponseNoHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            return await daprClient.InvokeMethodAsync<Widget>("app1", "mymethod");
        });

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Post);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);
        Assert.Null(request.Request.Content);

        var expected = new Widget()
        {
            Color = "red",
        };

        var actual = await request.CompleteWithJsonAsync(expected, jsonSerializerOptions);
        Assert.Equal(expected.Color, actual.Color);
    }

    [Fact]
    public async Task InvokeMethodAsync_VoidResponseWithHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            return await daprClient.InvokeMethodAsync<Widget>(HttpMethod.Put, "app1", "mymethod");
        });

        Assert.Equal(request.Request.Method, HttpMethod.Put);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);
        Assert.Null(request.Request.Content);

        var expected = new Widget()
        {
            Color = "red",
        };

        var actual = await request.CompleteWithJsonAsync(expected, jsonSerializerOptions);
        Assert.Equal(expected.Color, actual.Color);
    }

    [Fact]
    public async Task InvokeMethodAsync_RequestVoidNoHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var data = new Widget()
        {
            Color = "red",
        };

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            await daprClient.InvokeMethodAsync<Widget>("app1", "mymethod", data);
        });

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Post);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);

        var content = Assert.IsType<JsonContent>(request.Request.Content);
        Assert.Equal(data.GetType(), content.ObjectType);
        Assert.Same(data, content.Value);

        await request.CompleteAsync(new HttpResponseMessage());
    }

    [Fact]
    public async Task InvokeMethodAsync_RequestVoidWithHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var data = new Widget()
        {
            Color = "red",
        };

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            await daprClient.InvokeMethodAsync<Widget>(HttpMethod.Put, "app1", "mymethod", data);
        });

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Put);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);
            
        var content = Assert.IsType<JsonContent>(request.Request.Content);
        Assert.Equal(data.GetType(), content.ObjectType);
        Assert.Same(data, content.Value);

        await request.CompleteAsync(new HttpResponseMessage());
    }

    [Fact]
    public async Task InvokeMethodAsync_RequestResponseNoHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var data = new Widget()
        {
            Color = "red",
        };

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            return await daprClient.InvokeMethodAsync<Widget, Widget>("app1", "mymethod", data);
        });

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Post);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);

        var content = Assert.IsType<JsonContent>(request.Request.Content);
        Assert.Equal(data.GetType(), content.ObjectType);
        Assert.Same(data, content.Value);

        var actual = await request.CompleteWithJsonAsync(data, jsonSerializerOptions);
        Assert.Equal(data.Color, actual.Color);
    }

    [Fact]
    public async Task InvokeMethodAsync_RequestResponseWithHttpMethod_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var data = new Widget()
        {
            Color = "red",
        };

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            return await daprClient.InvokeMethodAsync<Widget, Widget>(HttpMethod.Put, "app1", "mymethod", data);
        });

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Put);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/app1/method/mymethod").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);
            
        var content = Assert.IsType<JsonContent>(request.Request.Content);
        Assert.Equal(data.GetType(), content.ObjectType);
        Assert.Same(data, content.Value);

        var actual = await request.CompleteWithJsonAsync(data, jsonSerializerOptions);
        Assert.Equal(data.Color, actual.Color);
    }
        
    [Fact]
    public async Task CheckHealthAsync_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync<bool>(async daprClient => 
            await daprClient.CheckHealthAsync());

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Get);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/healthz").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);

        var result = await request.CompleteAsync(new HttpResponseMessage());
        Assert.True(result);
    }
        
    [Fact]
    public async Task CheckHealthAsync_NotSuccess()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync<bool>(async daprClient => 
            await daprClient.CheckHealthAsync());

        // Get Request and validate
        Assert.Equal(request.Request.Method, HttpMethod.Get);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/healthz").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);

        var result = await request.CompleteAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Assert.False(result);
    }
        
    [Fact]
    public async Task CheckHealthAsync_WrapsHttpRequestException()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync<bool>(async daprClient => 
            await daprClient.CheckHealthAsync());

        Assert.Equal(request.Request.Method, HttpMethod.Get);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/healthz").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);
            
        var exception = new HttpRequestException();
        var result = await request.CompleteWithExceptionAndResultAsync(exception);
        Assert.False(result);
    }

    [Fact]
    public async Task CheckOutboundHealthAsync_Success()
    {
        await using var client = TestClient.CreateForDaprClient(c =>
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });
        var request = await client.CaptureHttpRequestAsync<bool>(async daprClient => await daprClient.CheckOutboundHealthAsync());

        Assert.Equal(request.Request.Method, HttpMethod.Get);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/healthz/outbound").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);

        var result = await request.CompleteAsync(new HttpResponseMessage());
        Assert.True(result);
    }

    [Fact]
    public async Task CheckOutboundHealthAsync_NotSuccess()
    {
        await using var client = TestClient.CreateForDaprClient(c =>
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });
        var request = await client.CaptureHttpRequestAsync<bool>(async daprClient => await daprClient.CheckOutboundHealthAsync());

        Assert.Equal(request.Request.Method, HttpMethod.Get);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/healthz/outbound").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);

        var result = await request.CompleteAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Assert.False(result);
    }

    [Fact]
    public async Task CheckOutboundHealthAsync_WrapsRequestException()
    {
        await using var client = TestClient.CreateForDaprClient(c =>
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });
        var request = await client.CaptureHttpRequestAsync<bool>(async daprClient => await daprClient.CheckOutboundHealthAsync());

        Assert.Equal(request.Request.Method, HttpMethod.Get);
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/healthz/outbound").AbsoluteUri, request.Request.RequestUri.AbsoluteUri);

        var result = await request.CompleteWithExceptionAndResultAsync(new HttpRequestException());
        Assert.False(result);
    }

    [Fact]
    public async Task WaitForSidecarAsync_SuccessWhenSidecarHealthy()
    {
        await using var client = TestClient.CreateForDaprClient();
        var request = await client.CaptureHttpRequestAsync(async daprClient => await daprClient.WaitForSidecarAsync());

        // If we don't throw, we're good.
        await request.CompleteAsync(new HttpResponseMessage());
    }

    [Fact]
    public async Task WaitForSidecarAsync_NotSuccessWhenSidecarNotHealthy()
    {
        await using var client = TestClient.CreateForDaprClient();
        using var cts = new CancellationTokenSource();
        var waitRequest = await client.CaptureHttpRequestAsync(async daprClient => await daprClient.WaitForSidecarAsync(cts.Token));
        var healthRequest = await client.CaptureHttpRequestAsync<bool>(async daprClient => await daprClient.CheckOutboundHealthAsync());

        cts.Cancel();

        await healthRequest.CompleteAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await waitRequest.CompleteWithExceptionAsync(new TaskCanceledException()));
    }

    [Fact]
    public async Task InvokeMethodAsync_WrapsHttpRequestException()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            var request = daprClient.CreateInvokeMethodRequest("test-app", "test");
            await daprClient.InvokeMethodAsync(request);
        });

        var exception = new HttpRequestException();
        var thrown = await Assert.ThrowsAsync<InvocationException>(async () => await request.CompleteWithExceptionAsync(exception));
        Assert.Equal("test-app", thrown.AppId);
        Assert.Equal("test", thrown.MethodName);
        Assert.Same(exception, thrown.InnerException);
        Assert.Null(thrown.Response);
    }

    [Fact]
    public async Task InvokeMethodAsync_WrapsHttpRequestException_FromEnsureSuccessStatus()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            var request = daprClient.CreateInvokeMethodRequest("test-app", "test");
            await daprClient.InvokeMethodAsync(request);
        });

        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var thrown = await Assert.ThrowsAsync<InvocationException>(async () => await request.CompleteAsync(response));
        Assert.Equal("test-app", thrown.AppId);
        Assert.Equal("test", thrown.MethodName);
        Assert.IsType<HttpRequestException>(thrown.InnerException);
        Assert.Same(response, thrown.Response);
    }

    [Fact]
    public async Task InvokeMethodAsync_WithBody_WrapsHttpRequestException()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            var request = daprClient.CreateInvokeMethodRequest("test-app", "test");
            return await daprClient.InvokeMethodAsync<Widget>(request);
        });

        var exception = new HttpRequestException();
        var thrown = await Assert.ThrowsAsync<InvocationException>(async () => await request.CompleteWithExceptionAsync(exception));
        Assert.Equal("test-app", thrown.AppId);
        Assert.Equal("test", thrown.MethodName);
        Assert.Same(exception, thrown.InnerException);
        Assert.Null(thrown.Response);
    }

    [Fact]
    public async Task InvokeMethodAsync_WithBody_WrapsHttpRequestException_FromEnsureSuccessStatus()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            var request = daprClient.CreateInvokeMethodRequest("test-app", "test");
            return await daprClient.InvokeMethodAsync<Widget>(request);
        });

            

        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var thrown = await Assert.ThrowsAsync<InvocationException>(async () => await request.CompleteAsync(response));
        Assert.Equal("test-app", thrown.AppId);
        Assert.Equal("test", thrown.MethodName);
        Assert.IsType<HttpRequestException>(thrown.InnerException);
        Assert.Same(response, thrown.Response);
    }

    [Fact]
    public async Task InvokeMethodAsync_WrapsHttpRequestException_FromSerialization()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            var request = daprClient.CreateInvokeMethodRequest("test-app", "test");
            return await daprClient.InvokeMethodAsync<Widget>(request);
        });

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ \"invalid\": true", Encoding.UTF8, "application/json")
        };
        var thrown = await Assert.ThrowsAsync<InvocationException>(async () => await request.CompleteAsync(response));
        Assert.Equal("test-app", thrown.AppId);
        Assert.Equal("test", thrown.MethodName);
        Assert.IsType<JsonException>(thrown.InnerException);
        Assert.Same(response, thrown.Response);
    }

    [Theory]
    [InlineData("", "https://test-endpoint:3501/v1.0/invoke/test-app/method/")]
    [InlineData("/", "https://test-endpoint:3501/v1.0/invoke/test-app/method/")]
    [InlineData("mymethod", "https://test-endpoint:3501/v1.0/invoke/test-app/method/mymethod")]
    [InlineData("/mymethod", "https://test-endpoint:3501/v1.0/invoke/test-app/method/mymethod")]
    [InlineData("mymethod?key1=value1&key2=value2#fragment", "https://test-endpoint:3501/v1.0/invoke/test-app/method/mymethod?key1=value1&key2=value2#fragment")]

    // garbage in -> garbage out - we don't deeply inspect what you pass.
    [InlineData("http://example.com", "https://test-endpoint:3501/v1.0/invoke/test-app/method/http://example.com")]
    public async Task CreateInvokeMethodRequest_TransformsUrlCorrectly(string method, string expected)
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = client.InnerClient.CreateInvokeMethodRequest("test-app", method);
        Assert.Equal(new Uri(expected).AbsoluteUri, request.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task CreateInvokeMethodRequest_AppendQueryStringValuesCorrectly()
    {
        await using var client = TestClient.CreateForDaprClient(c =>
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = client.InnerClient.CreateInvokeMethodRequest("test-app", "mymethod", (IReadOnlyCollection<KeyValuePair<string,string>>)new List<KeyValuePair<string, string>> { new("a", "0"), new("b", "1") });
        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/test-app/method/mymethod?a=0&b=1").AbsoluteUri, request.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task CreateInvokeMethodRequest_WithoutApiToken_CreatesHttpRequestWithoutApiTokenHeader()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c
                .UseGrpcEndpoint("http://localhost")
                .UseHttpEndpoint("https://test-endpoint:3501")
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .UseDaprApiToken(null);
        });

        var request = client.InnerClient.CreateInvokeMethodRequest("test-app", "test");
        Assert.False(request.Headers.TryGetValues("dapr-api-token", out _));
    }

    [Fact]
    public async Task CreateInvokeMethodRequest_WithApiToken_CreatesHttpRequestWithApiTokenHeader()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c
                .UseGrpcEndpoint("http://localhost")
                .UseHttpEndpoint("https://test-endpoint:3501")
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .UseDaprApiToken("test-token");
        });

        var request = client.InnerClient.CreateInvokeMethodRequest("test-app", "test");
        Assert.True(request.Headers.TryGetValues("dapr-api-token", out var values));
        Assert.Equal("test-token", Assert.Single(values));
    }

    [Fact]
    public async Task CreateInvokeMethodRequest_WithoutApiTokenAndWithData_CreatesHttpRequestWithoutApiTokenHeader()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c
                .UseGrpcEndpoint("http://localhost")
                .UseHttpEndpoint("https://test-endpoint:3501")
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .UseDaprApiToken(null);
        });

        var data = new Widget
        {
            Color = "red",
        };

        var request = client.InnerClient.CreateInvokeMethodRequest("test-app", "test", data);
        Assert.False(request.Headers.TryGetValues("dapr-api-token", out _));
    }

    [Fact]
    public async Task CreateInvokeMethodRequest_WithApiTokenAndData_CreatesHttpRequestWithApiTokenHeader()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c
                .UseGrpcEndpoint("http://localhost")
                .UseHttpEndpoint("https://test-endpoint:3501")
                .UseJsonSerializationOptions(this.jsonSerializerOptions)
                .UseDaprApiToken("test-token");
        });

        var data = new Widget
        {
            Color = "red",
        };

        var request = client.InnerClient.CreateInvokeMethodRequest("test-app", "test", data);
        Assert.True(request.Headers.TryGetValues("dapr-api-token", out var values));
        Assert.Equal("test-token", Assert.Single(values));
    }

    [Fact]
    public async Task CreateInvokeMethodRequest_WithData_CreatesJsonContent()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var data = new Widget
        {
            Color = "red",
        };

        var request = client.InnerClient.CreateInvokeMethodRequest("test-app", "test", data);
        var content = Assert.IsType<JsonContent>(request.Content);
        Assert.Equal(typeof(Widget), content.ObjectType);
        Assert.Same(data, content.Value);

        // the best way to verify the usage of the correct settings object
        var actual = await content.ReadFromJsonAsync<Widget>(this.jsonSerializerOptions);
        Assert.Equal(data.Color, actual.Color);
    }

    [Fact]
    public async Task CreateInvokeMethodRequest_WithData_CreatesJsonContentWithQueryString()
    {
        await using var client = TestClient.CreateForDaprClient(c =>
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var data = new Widget
        {
            Color = "red",
        };

        var request = client.InnerClient.CreateInvokeMethodRequest(HttpMethod.Post, "test-app", "test", new List<KeyValuePair<string, string>> { new("a", "0"), new("b", "1") }, data);

        Assert.Equal(new Uri("https://test-endpoint:3501/v1.0/invoke/test-app/method/test?a=0&b=1").AbsoluteUri, request.RequestUri.AbsoluteUri);

        var content = Assert.IsType<JsonContent>(request.Content);
        Assert.Equal(typeof(Widget), content.ObjectType);
        Assert.Same(data, content.Value);

        // the best way to verify the usage of the correct settings object
        var actual = await content.ReadFromJsonAsync<Widget>(this.jsonSerializerOptions);
        Assert.Equal(data.Color, actual.Color);
    }
        
    [Fact]
    public async Task InvokeMethodWithoutResponse_WithExtraneousHeaders()
    {
        await using var client = TestClient.CreateForDaprClient(c =>
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var req = await client.CaptureHttpRequestAsync(async DaprClient =>
        {
            var request = client.InnerClient.CreateInvokeMethodRequest(HttpMethod.Get, "test-app", "mymethod");
            request.Headers.Add("test-api-key", "test");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "abc123");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await DaprClient.InvokeMethodAsync(request);
        });

        req.Dismiss();
            
        Assert.NotNull(req);
        Assert.True(req.Request.Headers.Contains("test-api-key"));
        Assert.Equal("test", req.Request.Headers.GetValues("test-api-key").First());
        Assert.True(req.Request.Headers.Contains("Authorization"));
        Assert.Equal("Bearer abc123", req.Request.Headers.GetValues("Authorization").First());
        Assert.Equal("application/json", req.Request.Headers.GetValues("Accept").First());
    }

    [Fact]
    public async Task InvokeMethodWithResponseAsync_ReturnsMessageWithoutCheckingStatus()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            var request = daprClient.CreateInvokeMethodRequest("test-app", "test");
            return await daprClient.InvokeMethodWithResponseAsync(request);
        });

        var response = await request.CompleteAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)); // Non-2xx response
        Assert.NotNull(response);
    }

    [Fact]
    public async Task InvokeMethodWithResponseAsync_WrapsHttpRequestException()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = await client.CaptureHttpRequestAsync(async daprClient =>
        {
            var request = daprClient.CreateInvokeMethodRequest("test-app", "test");
            return await daprClient.InvokeMethodWithResponseAsync(request);
        });

        var exception = new HttpRequestException();
        var thrown = await Assert.ThrowsAsync<InvocationException>(async () => await request.CompleteWithExceptionAsync(exception));
        Assert.Equal("test-app", thrown.AppId);
        Assert.Equal("test", thrown.MethodName);
        Assert.Same(exception, thrown.InnerException);
        Assert.Null(thrown.Response);
    }

    [Fact]
    public async Task InvokeMethodWithResponseAsync_PreventsNonDaprRequest()
    {
        await using var client = TestClient.CreateForDaprClient(c => 
        {
            c.UseGrpcEndpoint("http://localhost").UseHttpEndpoint("https://test-endpoint:3501").UseJsonSerializationOptions(this.jsonSerializerOptions);
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
        {
            await client.InnerClient.InvokeMethodWithResponseAsync(request);
        });

        Assert.Equal("The provided request URI is not a Dapr service invocation URI.", ex.Message);
    }

    private class Widget
    {
        public string Color { get; set; }
    }
}