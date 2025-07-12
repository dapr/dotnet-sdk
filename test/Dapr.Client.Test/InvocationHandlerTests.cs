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
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Dapr.Client;

public class InvocationHandlerTests
{
    [Fact]
    public void DaprEndpoint_InvalidScheme()
    {
        var handler = new InvocationHandler();
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            handler.DaprEndpoint = "ftp://localhost:3500";
        });

        Assert.Contains("The URI scheme of the Dapr endpoint must be http or https.", ex.Message);
    }

    [Fact]
    public void DaprEndpoint_InvalidUri()
    {
        var handler = new InvocationHandler();
        Assert.Throws<UriFormatException>(() =>
        {
            handler.DaprEndpoint = "";
        });

        // Exception message comes from the runtime, not validating it here
    }

    [Fact]
    public void TryRewriteUri_FailsForNullUri()
    {
        var handler = new InvocationHandler();
        Assert.False(handler.TryRewriteUri(null!, out var rewritten));
        Assert.Null(rewritten);
    }

    [Fact]
    public void TryRewriteUri_FailsForBadScheme()
    {
        var uri = new Uri("ftp://test", UriKind.Absolute);

        var handler = new InvocationHandler();
        Assert.False(handler.TryRewriteUri(uri, out var rewritten));
        Assert.Null(rewritten);
    }

    [Fact]
    public void TryRewriteUri_FailsForRelativeUris()
    {
        var uri = new Uri("test", UriKind.Relative);

        var handler = new InvocationHandler();
        Assert.False(handler.TryRewriteUri(uri, out var rewritten));
        Assert.Null(rewritten);
    }

    [Theory]
    [InlineData(null, "http://bank", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("bank", "http://bank", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("Bank", "http://bank", "https://some.host:3499/v1.0/invoke/Bank/method/")]
    [InlineData("invalid", "http://bank", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData(null, "http://Bank", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("Bank", "http://Bank", "https://some.host:3499/v1.0/invoke/Bank/method/")]
    [InlineData("bank", "http://Bank", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("invalid", "http://Bank", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData(null, "http://bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("bank", "http://bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("invalid", "http://bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData(null, "http://Bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("Bank", "http://Bank:3939", "https://some.host:3499/v1.0/invoke/Bank/method/")]
    [InlineData("invalid", "http://Bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData(null, "http://app-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/")]
    [InlineData("app-id.with.dots", "http://app-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/")]
    [InlineData("invalid", "http://app-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/")]
    [InlineData(null, "http://App-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/")]
    [InlineData("App-id.with.dots", "http://App-id.with.dots", "https://some.host:3499/v1.0/invoke/App-id.with.dots/method/")]
    [InlineData("invalid", "http://App-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/")]
    [InlineData(null, "http://bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("bank", "http://bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("invalid", "http://bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData(null, "http://Bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData("Bank", "http://Bank:3939/", "https://some.host:3499/v1.0/invoke/Bank/method/")]
    [InlineData("invalid", "http://Bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/")]
    [InlineData(null, "http://bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path")]
    [InlineData("bank", "http://bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path")]
    [InlineData("invalid", "http://bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path")]
    [InlineData(null, "http://Bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path")]
    [InlineData("Bank", "http://Bank:3939/some/path", "https://some.host:3499/v1.0/invoke/Bank/method/some/path")]
    [InlineData("invalid", "http://Bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path")]
    [InlineData(null, "http://bank:3939/some/path?q=test&p=another#fragment", "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment")]
    [InlineData("bank", "http://bank:3939/some/path?q=test&p=another#fragment", "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment")]
    [InlineData("invalid", "http://bank:3939/some/path?q=test&p=another#fragment", "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment")]
    [InlineData(null, "http://Bank:3939/some/path?q=test&p=another#fragment", "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment")]
    [InlineData("Bank", "http://Bank:3939/some/path?q=test&p=another#fragment", "https://some.host:3499/v1.0/invoke/Bank/method/some/path?q=test&p=another#fragment")]
    [InlineData("invalid", "http://Bank:3939/some/path?q=test&p=another#fragment", "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment")]
    public void TryRewriteUri_WithNoAppId_RewritesUriToDaprInvoke(string? appId, string uri, string expected)
    {
        var handler = new InvocationHandler()
        {
            DaprEndpoint = "https://some.host:3499",
            DefaultAppId = appId,
        };

        Assert.True(handler.TryRewriteUri(new Uri(uri), out var rewritten));
        Assert.Equal(expected, rewritten!.OriginalString);
    }

    [Fact]
    public async Task SendAsync_InvalidNotSetUri_ThrowsException()
    {
        var handler = new InvocationHandler();
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await CallSendAsync(handler, new HttpRequestMessage() { }); // No URI set
        });

        Assert.Contains("The request URI '' is not a valid Dapr service invocation destination.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_RewritesUri()
    {
        var uri = "http://bank/accounts/17?";

        var capture = new CaptureHandler();
        var handler = new InvocationHandler()
        {
            InnerHandler = capture,

            DaprEndpoint = "https://localhost:5000",
            DaprApiToken = null,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        await CallSendAsync(handler, request);

        Assert.Equal("https://localhost:5000/v1.0/invoke/bank/method/accounts/17?", capture.RequestUri?.OriginalString);
        Assert.Null(capture.DaprApiToken);

        Assert.Equal(uri, request.RequestUri?.OriginalString);
        Assert.False(request.Headers.TryGetValues("dapr-api-token", out _));
    }

    [Fact]
    public async Task SendAsync_RewritesUri_AndAppId()
    {
        var uri = "http://bank/accounts/17?";

        var capture = new CaptureHandler();
        var handler = new InvocationHandler()
        {
            InnerHandler = capture,

            DaprEndpoint = "https://localhost:5000",
            DaprApiToken = null,
            DefaultAppId = "Bank"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        await CallSendAsync(handler, request);

        Assert.Equal("https://localhost:5000/v1.0/invoke/Bank/method/accounts/17?", capture.RequestUri?.OriginalString);
        Assert.Null(capture.DaprApiToken);

        Assert.Equal(uri, request.RequestUri?.OriginalString);
        Assert.False(request.Headers.TryGetValues("dapr-api-token", out _));
    }

    [Fact]
    public async Task SendAsync_RewritesUri_AndAddsApiToken()
    {
        var uri = "http://bank/accounts/17?";

        var capture = new CaptureHandler();
        var handler = new InvocationHandler()
        {
            InnerHandler = capture,

            DaprEndpoint = "https://localhost:5000",
            DaprApiToken = "super-duper-secure",
        };

        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        await CallSendAsync(handler, request);

        Assert.Equal("https://localhost:5000/v1.0/invoke/bank/method/accounts/17?", capture.RequestUri?.OriginalString);
        Assert.Equal("super-duper-secure", capture.DaprApiToken);

        Assert.Equal(uri, request.RequestUri?.OriginalString);
        Assert.False(request.Headers.TryGetValues("dapr-api-token", out _));
    }

    private async Task<HttpResponseMessage> CallSendAsync(InvocationHandler handler, HttpRequestMessage message, CancellationToken cancellationToken = default)
    {
        // SendAsync is protected, can't call it directly.
        var method = handler.GetType().GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        try
        {
            return await (Task<HttpResponseMessage>)method!.Invoke(handler, new object[] { message, cancellationToken, })!;
        }
        catch (TargetInvocationException tie) // reflection always adds an extra layer of exceptions.
        {
            throw tie.InnerException!;
        }
    }

    private class CaptureHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? DaprApiToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            if (request.Headers.TryGetValues("dapr-api-token", out var tokens))
            {
                DaprApiToken = tokens.SingleOrDefault();
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}